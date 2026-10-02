using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Itachi.ExcuseMeDrone
{
    internal static class ExcuseMeDroneController
    {
        private sealed class RuntimeState
        {
            internal float NextThreatScanTime;
            internal float NextCombatDodgeTime;
            internal Vector3 LastSamplePosition;
            internal float LastSampleTime;
            internal float StuckAccumulated;
            internal bool HasSample;
            internal bool PlacementBlockedLogged;
            internal float NextRescueAttemptTime;
            internal bool PendingBrokenRevive;
            internal bool PendingBrokenReshutdown;
            internal float PendingBrokenStartY;
            internal float PendingBrokenStartedAt;
            internal int PendingBrokenStartFrame;
            internal bool PendingBrokenGroundTeleportApplied;
            internal float PendingBrokenGroundY;
            internal readonly List<Entity> ThreatCandidates = new List<Entity>();
        }

        private static readonly Dictionary<int, RuntimeState> States = new Dictionary<int, RuntimeState>();
        private static readonly Collider[] PlacementOverlaps = new Collider[32];
        private static EntityDrone localDrone;
        private static int lastSummonFrame = -1;
        private static bool loggedFirstLocalDroneTick;
        private static bool loggedFirstUiTick;

        internal static void Reset()
        {
            States.Clear();
            localDrone = null;
            lastSummonFrame = -1;
            loggedFirstLocalDroneTick = false;
            loggedFirstUiTick = false;
        }

        internal static void TickBeforeVanilla(EntityDrone drone)
        {
            if (drone == null) return;
            EntityPlayerLocal owner = drone.Owner as EntityPlayerLocal;
            if (owner == null) return;

            if (!loggedFirstLocalDroneTick)
            {
                loggedFirstLocalDroneTick = true;
                Debug.Log("[ExcuseMeDrone] EntityDrone.OnUpdateEntity patch active. Local drone=" + drone.entityId + ", order=" + drone.OrderState);
            }

            localDrone = drone;
            RuntimeState state = GetState(drone);
            ExcuseMeDroneConfig cfg = ExcuseMeDroneConfig.Current;

            // Broken-drone F10 summon is two-stage. First move the still-shutdown
            // entity near the player. Only after the nearby entity has resumed ticking
            // do we temporarily wake it. This avoids mutating Health while distant
            // render/physics objects may still be unavailable.
            if (state.PendingBrokenRevive)
            {
                UpdatePendingBrokenRevive(drone, state);
                ResetStuckSample(state);
                return;
            }

            if (state.PendingBrokenReshutdown)
            {
                UpdatePendingBrokenReshutdown(drone, state);
                ResetStuckSample(state);
                return;
            }

            // A shutdown/broken drone may still be summoned manually with F10, but
            // automatic combat dodge and stuck rescue must not keep moving it.
            if (IsShutdownOrBroken(drone))
            {
                ResetStuckSample(state);
                return;
            }

            if (!IsFollowOrder(drone))
            {
                ResetStuckSample(state);
                return;
            }

            if (cfg.CombatDodgeEnabled)
                UpdateCombatDodge(drone, owner, state, cfg);

            if (cfg.StuckRescueEnabled)
                UpdateStuckRescue(drone, owner, state, cfg);
            else
                ResetStuckSample(state);
        }

        internal static void TickAfterVanilla(EntityDrone drone)
        {
            if (drone == null) return;
            if (!(drone.Owner is EntityPlayerLocal)) return;

            if (ExcuseMeDroneConfig.Current.Invisible)
                drone.SetRenderersEnabled(false);
        }

        internal static void HandleSummonKey(LocalPlayerUI ui)
        {
            if (ui == null || ui.entityPlayer == null) return;

            ExcuseMeDroneConfig cfg = ExcuseMeDroneConfig.Current;
            if (!loggedFirstUiTick)
            {
                loggedFirstUiTick = true;
                Debug.Log("[ExcuseMeDrone] LocalPlayerUI.Update patch active. SummonKey=" + cfg.SummonKey);
            }

            if (!Input.GetKeyDown(cfg.SummonKey)) return;
            if (lastSummonFrame == Time.frameCount) return;
            lastSummonFrame = Time.frameCount;

            EntityPlayerLocal player = ui.entityPlayer;
            if (GameManager.Instance == null || GameManager.Instance.World == null) return;
            World world = GameManager.Instance.World;
            EntityDrone drone = localDrone;
            if (drone == null || drone.Owner != player || world.GetEntity(drone.entityId) != drone)
            {
                drone = null;
                if (DroneManager.Instance != null)
                {
                    foreach (EntityCreationData saved in DroneManager.Instance.GetAllDronesECD())
                    {
                        if (saved.belongsPlayerId != player.entityId) continue;
                        EntityDrone active = world.GetEntity(saved.id) as EntityDrone;
                        if (active != null && active.Owner == player)
                        {
                            drone = active;
                            localDrone = active;
                            break;
                        }
                    }
                }
            }
            if (drone == null)
            {
                HandleUnloadedSummon(ui, player, world, cfg);
                return;
            }

            // Do not replace an existing confirmation or accept repeated hotkeys
            // while the player is deciding. Other inventory/menu UI stays allowed.
            if (ui.xui == null || XUiC_MessageBoxWindowGroup.IsShowing(ui.xui)) return;

            if ((int)drone.OrderState == 1)
            {
                XUiC_MessageBoxWindowGroup.ShowOkCancel(ui.xui,
                    "Excuse Me, Drone", "The drone is in Stay mode. Switch to Follow and teleport it to you?", "",
                    delegate
                    {
                        // The world, owner or order may have changed while the
                        // confirmation was open. Never act on a stale drone.
                        if (ui.entityPlayer != player || localDrone != drone || drone.Owner != player ||
                            (int)drone.OrderState != 1 || GameManager.Instance == null ||
                            GameManager.Instance.World == null ||
                            GameManager.Instance.World.GetEntity(drone.entityId) != drone) return;
                        drone.FollowMode();
                        SummonDrone(drone, player, ExcuseMeDroneConfig.Current);
                    }, delegate { }, false, true, true);
                return;
            }

            if (!IsFollowOrder(drone))
            {
                DebugLog("Summon denied: unsupported drone order.");
                return;
            }

            SummonDrone(drone, player, cfg);
        }

        private static void HandleUnloadedSummon(LocalPlayerUI ui, EntityPlayerLocal player,
            World world, ExcuseMeDroneConfig cfg)
        {
            if (ui.xui == null || XUiC_MessageBoxWindowGroup.IsShowing(ui.xui)) return;
            // Only the server owns the saved drone records and may spawn entities.
            if (!ConnectionManager.Instance.IsServer || DroneManager.Instance == null)
            {
                DebugLog("No loaded owned drone; unloaded recall requires the server.");
                return;
            }
            EntityCreationData candidate = null;
            foreach (EntityCreationData saved in DroneManager.Instance.GetAllDronesECD())
            {
                if (saved.belongsPlayerId == player.entityId && world.GetEntity(saved.id) == null)
                {
                    candidate = saved;
                    break;
                }
            }
            if (candidate == null)
            {
                DebugLog("Summon key pressed, but no owned drone is registered or saved.");
                return;
            }
            EntityCreationData selected = candidate;
            if ((int)selected.orderState == 1)
            {
                XUiC_MessageBoxWindowGroup.ShowOkCancel(ui.xui,
                    "Excuse Me, Drone", "The drone is in Stay mode. Switch to Follow and teleport it to you?", "",
                    delegate
                    {
                        if (ui.entityPlayer != player || GameManager.Instance == null ||
                            GameManager.Instance.World != world || (int)selected.orderState != 1) return;
                        RecallUnloadedDrone(selected, player, world, ExcuseMeDroneConfig.Current);
                    }, delegate { }, false, true, true);
                return;
            }
            if ((int)selected.orderState == 0)
                RecallUnloadedDrone(selected, player, world, cfg);
        }

        private static void RecallUnloadedDrone(EntityCreationData selected, EntityPlayerLocal player,
            World world, ExcuseMeDroneConfig cfg)
        {
            if (!ConnectionManager.Instance.IsServer || selected.belongsPlayerId != player.entityId ||
                world.GetEntity(selected.id) != null || DroneManager.Instance == null) return;
            // Revalidate the actual saved record; cancellation never alters it.
            bool registered = false;
            foreach (EntityCreationData saved in DroneManager.Instance.GetAllDronesECD())
                if (object.ReferenceEquals(saved, selected)) { registered = true; break; }
            if (!registered) return;
            Vector3 previousPosition = selected.pos;
            selected.pos = FindManualSummonTarget(player, null, cfg, true);
            EntityDrone restored = DroneManager.Instance.LoadDrone(selected.id, world);
            if (restored == null)
            {
                selected.pos = previousPosition;
                DebugLog("Saved drone could not be loaded for recall.");
                return;
            }
            localDrone = restored;
            restored.FollowMode();
            SummonDrone(restored, player, cfg);
            // DroneManager.Update removes the now-loaded record from its unloaded list.
            DebugLog("Recalled saved drone " + restored.entityId + " near its owner.");
        }

        private static void SummonDrone(EntityDrone drone, EntityPlayerLocal player, ExcuseMeDroneConfig cfg)
        {
            float distance = Vector3.Distance(drone.position, player.position);
            RuntimeState runtimeState = GetState(drone);
            bool wasBroken = IsShutdownOrBroken(drone);

            Vector3 summonTarget = FindManualSummonTarget(player, drone, cfg, wasBroken);
            drone.TeleportToPosition(summonTarget);
            ResetStuckSample(runtimeState);

            if (wasBroken)
            {
                runtimeState.PendingBrokenRevive = true;
                runtimeState.PendingBrokenReshutdown = false;
                runtimeState.PendingBrokenStartY = summonTarget.y;
                runtimeState.PendingBrokenStartedAt = Time.time;
                runtimeState.PendingBrokenStartFrame = Time.frameCount;
                runtimeState.PendingBrokenGroundTeleportApplied = false;
                runtimeState.PendingBrokenGroundY = 0f;
                DebugLog("Broken drone summoned to player-relative safe position; revive pending.");
            }
            else
            {
                DebugLog("Summon: teleported drone from " + distance.ToString("F1") +
                    "m to player-front ground target y=" + summonTarget.y.ToString("F2") + ".");
            }
        }


        private static void UpdatePendingBrokenRevive(EntityDrone drone, RuntimeState state)
        {
            // Do not wake in the same frame as TeleportToPosition. The whole point of
            // this phase is to let the nearby entity rebuild/resume its Unity objects.
            if (Time.frameCount <= state.PendingBrokenStartFrame)
                return;

            if (IsBrokenReviveReady(drone) && PrepareBrokenDroneForGroundSummon(drone))
            {
                state.PendingBrokenRevive = false;
                state.PendingBrokenReshutdown = true;
                state.PendingBrokenStartedAt = Time.time;
                state.PendingBrokenStartFrame = Time.frameCount;
                state.PendingBrokenGroundTeleportApplied = false;
                state.PendingBrokenGroundY = 0f;
                DebugLog("Broken drone temporary revive applied.");
                return;
            }

            // Retry while the teleported entity is becoming active near the player.
            // Health is not changed until Vanilla's shutdown-clear path succeeds.
            if (Time.time - state.PendingBrokenStartedAt >= 3.0f)
            {
                state.PendingBrokenRevive = false;
                DebugLog("Broken-drone temporary revive timed out.");
            }
        }

        private static bool IsBrokenReviveReady(EntityDrone drone)
        {
            try
            {
                Type type = drone.GetType();
                while (type != null)
                {
                    FieldInfo field = type.GetField("PhysicsTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        Transform physicsTransform = field.GetValue(drone) as Transform;
                        return physicsTransform != null;
                    }
                    type = type.BaseType;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool PrepareBrokenDroneForGroundSummon(EntityDrone drone)
        {
            try
            {
                // Vanilla repair order is Health first, then setShutdown(false).
                // Earlier tests also showed that this order can revive a nearby broken
                // drone, whereas reversing it does not. The entity has already been
                // moved near the player and allowed at least one update before this runs.
                drone.Health = 2;

                // These are public in v3.2. Call the game's API directly instead of
                // reflecting them as non-public methods.
                drone.setShutdown(false);

                // Vanilla performRepair() calls this immediately after setShutdown(false).
                // Keep the wake-up animation/state transition without performing a full repair.
                drone.playWakeupAnim();
                return true;
            }
            catch (Exception ex)
            {
                RollbackBrokenRevive(drone);
                DebugLog("Temporary revive failed: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private static void RollbackBrokenRevive(EntityDrone drone)
        {
            try
            {
                drone.Health = 1;
                drone.performShutdown();
            }
            catch
            {
            }
        }

        private static void UpdatePendingBrokenReshutdown(EntityDrone drone, RuntimeState state)
        {
            // Let at least one complete Vanilla update run after wake-up. Then force the
            // now-live drone onto the ground once. The normal drone AI must lift it back
            // up before we restore the broken/shutdown state. This reproduces the redraw
            // cycle observed with a healthy drone: ground placement -> self-powered lift.
            if (Time.frameCount <= state.PendingBrokenStartFrame)
                return;

            ExcuseMeDroneConfig cfg = ExcuseMeDroneConfig.Current;

            if (!state.PendingBrokenGroundTeleportApplied)
            {
                EntityPlayerLocal owner = drone.Owner as EntityPlayerLocal;
                if (owner == null) return;
                Vector3 groundTarget = FindGroundRedrawTarget(owner, drone.position, drone, cfg);
                // Set the applied flag only after the redraw teleport actually runs.
                drone.TeleportToPosition(groundTarget);

                state.PendingBrokenGroundTeleportApplied = true;
                state.PendingBrokenGroundY = groundTarget.y;
                state.PendingBrokenStartedAt = Time.time;
                state.PendingBrokenStartFrame = Time.frameCount;
                DebugLog("Broken drone ground redraw cycle started.");
                return;
            }

            // Do not count the same frame as the ground teleport. Vanilla gets a full
            // update to begin its normal hover/lift behavior first.
            if (Time.frameCount <= state.PendingBrokenStartFrame)
                return;

            float awakeSeconds = Time.time - state.PendingBrokenStartedAt;
            float upwardDelta = drone.position.y - state.PendingBrokenGroundY;
            bool lifted = upwardDelta >= cfg.BrokenLiftThreshold;
            bool timedOut = awakeSeconds >= cfg.BrokenReviveTimeoutSeconds;

            // Even after a visible lift, leave the drone alive briefly so rendering and
            // normal movement state can settle before shutdown is restored.
            if (awakeSeconds < cfg.BrokenMinimumAwakeSeconds)
                return;

            if (!lifted && !timedOut)
                return;

            bool shutdownApplied = ApplyBrokenDroneSuicide(drone);
            state.PendingBrokenReshutdown = false;
            state.PendingBrokenGroundTeleportApplied = false;
            state.PendingBrokenGroundY = 0f;

            if (!shutdownApplied)
            {
                DebugLog("Broken-drone shutdown restore failed.");
                return;
            }

            if (lifted)
                DebugLog("Broken drone re-lifted; shutdown restored.");
            else
                DebugLog("Broken-drone re-lift timed out; shutdown restored.");
        }

        private static bool ApplyBrokenDroneSuicide(EntityDrone drone)
        {
            try
            {
                // Vanilla Health never reaches 0: the setter clamps to 1 and marks shutdown
                // pending. performShutdown() is the game's own destruction/shutdown path.
                drone.Health = 1;
                drone.performShutdown();
                return true;
            }
            catch (Exception ex)
            {
                DebugLog("Shutdown restore failed: " + ex.GetType().Name + ".");
                return false;
            }
        }

        private static RuntimeState GetState(EntityDrone drone)
        {
            RuntimeState state;
            if (!States.TryGetValue(drone.entityId, out state))
            {
                state = new RuntimeState();
                States[drone.entityId] = state;
            }
            return state;
        }

        private static bool IsFollowOrder(EntityDrone drone)
        {
            // v3.2 EntityDrone: FollowMode -> setOrders(0), Sentry/Stay -> setOrders(1).
            return (int)drone.OrderState == 0;
        }

        private static bool IsShutdownOrBroken(EntityDrone drone)
        {
            // v3.2 Health clamps to a minimum of 1; Health <= 1 or state 5 is shutdown/broken.
            return drone.Health <= 1 || (int)drone.GetState() == 5;
        }

        private static void UpdateCombatDodge(EntityDrone drone, EntityPlayerLocal owner, RuntimeState state, ExcuseMeDroneConfig cfg)
        {
            float now = Time.time;

            // The cooldown suppresses ONLY another forced dodge. Vanilla follow/attack AI
            // continues normally immediately after the teleport.
            if (now < state.NextCombatDodgeTime) return;
            if (now < state.NextThreatScanTime) return;
            state.NextThreatScanTime = now + cfg.ThreatScanInterval;

            EntityAlive enemy;
            float distance;
            if (!TryFindNearestZombie(owner, state, cfg.CombatTriggerDistance, out enemy, out distance))
                return;

            Vector3 target;
            if (!TryFindTeleportTarget(owner, ComputeDodgeAnchor(owner, cfg), drone, cfg,
                cfg.GroundSnapEnabled, out target))
            {
                if (!state.PlacementBlockedLogged)
                    DebugLog("Automatic teleport skipped: no clear position on the owner's current floor.");
                state.PlacementBlockedLogged = true;
                return;
            }
            state.PlacementBlockedLogged = false;
            drone.TeleportToPosition(target);
            state.NextCombatDodgeTime = now + cfg.CombatDodgeCooldownSeconds;
            ResetStuckSample(state);

            DebugLog("Drone " + drone.entityId + " combat dodge: zombie " + enemy.entityId +
                " at " + distance.ToString("F1") + "m; cooldown " + cfg.CombatDodgeCooldownSeconds.ToString("F0") + "s.");
        }

        private static bool TryFindNearestZombie(EntityPlayerLocal owner, RuntimeState state, float radius, out EntityAlive nearest, out float nearestDistance)
        {
            nearest = null;
            nearestDistance = float.MaxValue;

            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return false;

            List<Entity> candidates = state.ThreatCandidates;
            candidates.Clear();
            GameManager.Instance.World.GetEntitiesAround((EntityFlags)15, owner.position, radius, candidates);

            float nearestSq = radius * radius;
            for (int i = 0; i < candidates.Count; i++)
            {
                EntityAlive alive = candidates[i] as EntityAlive;
                if (!IsZombieThreat(alive)) continue;

                Vector3 delta = alive.position - owner.position;
                float sq = delta.sqrMagnitude;
                if (sq > nearestSq) continue;

                nearestSq = sq;
                nearest = alive;
            }

            if (nearest == null) return false;
            nearestDistance = Mathf.Sqrt(nearestSq);
            return true;
        }

        private static bool IsZombieThreat(EntityAlive entity)
        {
            if (entity == null || entity.IsDead()) return false;

            Type type = entity.GetType();
            while (type != null)
            {
                if (type.Name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private static void UpdateStuckRescue(EntityDrone drone, EntityPlayerLocal owner, RuntimeState state, ExcuseMeDroneConfig cfg)
        {
            if (Time.time < state.NextRescueAttemptTime) return;
            float ownerDistance = Vector3.Distance(drone.position, owner.position);
            if (ownerDistance >= cfg.ImmediateRescueDistance)
            {
                Rescue(drone, owner, state, cfg, "distance " + ownerDistance.ToString("F1") + "m");
                return;
            }

            float now = Time.time;
            if (!state.HasSample)
            {
                state.HasSample = true;
                state.LastSamplePosition = drone.position;
                state.LastSampleTime = now;
                state.StuckAccumulated = 0f;
                return;
            }

            float elapsed = now - state.LastSampleTime;
            if (elapsed < 1.0f) return;

            float moved = Vector3.Distance(drone.position, state.LastSamplePosition);
            bool separatedVertically = Mathf.Abs(drone.position.y - owner.position.y) >= 3.0f;
            if ((ownerDistance >= cfg.StuckDistance || separatedVertically) && moved <= cfg.StuckMovementThreshold)
                state.StuckAccumulated += elapsed;
            else
                state.StuckAccumulated = 0f;

            state.LastSamplePosition = drone.position;
            state.LastSampleTime = now;

            if (state.StuckAccumulated >= cfg.StuckSeconds)
                Rescue(drone, owner, state, cfg, "stuck " + state.StuckAccumulated.ToString("F1") + "s at " + ownerDistance.ToString("F1") + "m");
        }

        private static void Rescue(EntityDrone drone, EntityPlayerLocal owner, RuntimeState state, ExcuseMeDroneConfig cfg, string reason)
        {
            state.NextRescueAttemptTime = Time.time + 1.0f;
            Vector3 target;
            if (!TryFindTeleportTarget(owner, ComputeDodgeAnchor(owner, cfg), drone, cfg,
                cfg.GroundSnapEnabled, out target))
            {
                if (!state.PlacementBlockedLogged)
                    DebugLog("Automatic teleport skipped: no clear position on the owner's current floor.");
                state.PlacementBlockedLogged = true;
                return;
            }
            state.PlacementBlockedLogged = false;
            drone.TeleportToPosition(target);
            ResetStuckSample(state);
            DebugLog("Drone " + drone.entityId + " rescue (" + reason + ").");
        }

        private static Vector3 ComputeSummonAnchor(EntityPlayerLocal owner, ExcuseMeDroneConfig cfg)
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            return owner.position
                + forward * cfg.SummonForwardDistance
                + Vector3.up * cfg.SummonHeightOffset;
        }


        private static Vector3 FindManualSummonTarget(EntityPlayerLocal owner, EntityDrone drone,
            ExcuseMeDroneConfig cfg, bool broken)
        {
            Vector3 desired = broken ? ComputeBrokenSummonTarget(owner, cfg) : ComputeSummonAnchor(owner, cfg);
            if (!broken && cfg.GroundSnapEnabled)
                return FindGroundRedrawTarget(owner, desired, drone, cfg);
            Vector3 target;
            if (TryFindTeleportTarget(owner, desired, drone, cfg, false, out target))
                return target;


            // The player's occupied space is the final fallback, rather than the same
            // blocked forward offset or a terrain/roof height. Vanilla separates the drone.
            DebugLog("Manual recall: no clear nearby candidate; using player-relative fallback.");
            return owner.position + Vector3.up * cfg.BrokenSummonHeightOffset;
        }

        private static Vector3 FindGroundRedrawTarget(EntityPlayerLocal owner, Vector3 desired,
            EntityDrone drone, ExcuseMeDroneConfig cfg)
        {
            if (!cfg.GroundSnapEnabled) return desired;
            Vector3 target;
            if (TryFindTeleportTarget(owner, desired, drone, cfg, true, out target))
                return target;

            // Preserve the ground -> Vanilla lift redraw path even when the physics
            // search rejects all candidates. The owner's foot position supplies the
            // current-floor baseline; do not substitute airborne placement or a
            // global height map, and do not mark the ground teleport as applied early.
            DebugLog("Ground redraw: local floor search failed; using owner-foot fallback.");
            return owner.position + Vector3.up * cfg.GroundClearance;
        }

        private static bool TryFindTeleportTarget(EntityPlayerLocal owner, Vector3 desired, EntityDrone drone,
            ExcuseMeDroneConfig cfg, bool ground, out Vector3 target)
        {
            if (TryPlacement(owner, desired, drone, cfg, ground, false, out target))
                return true;

            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            else forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            // Search around the owner, not around a point which may lie through a wall.
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = ring == 0 ? 1.5f : ring == 1 ? 1.0f : 0.6f;
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    Vector3 candidate = owner.position +
                        (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius;
                    candidate.y = desired.y;
                    if (TryPlacement(owner, candidate, drone, cfg, ground, false, out target))
                        return true;
                }
            }

            Vector3 atOwner = owner.position;
            atOwner.y = desired.y;
            // Allow brief owner overlap only for the final center candidate. Do not
            // ignore walls, ceilings, other entities or other drones.
            return TryPlacement(owner, atOwner, drone, cfg, ground, true, out target);
        }

        private static bool TryPlacement(EntityPlayerLocal owner, Vector3 desired, EntityDrone drone,
            ExcuseMeDroneConfig cfg, bool ground, bool allowOwnerOverlap, out Vector3 target)
        {
            target = desired;
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return false;

            // Unity physics uses floating-origin coordinates; entity positions and
            // block queries use absolute world coordinates.
            Vector3 origin = Origin.position;
            if (ground)
            {
                Vector3 rayStart = new Vector3(desired.x, owner.position.y + 0.75f, desired.z);
                RaycastHit[] hits = Physics.RaycastAll(rayStart - origin, Vector3.down, 1.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                bool found = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider == null || IsEntityCollider(hit.collider, drone) ||
                        IsEntityCollider(hit.collider, owner) || hit.normal.y < 0.5f) continue;
                    Vector3 point = hit.point + origin;
                    // Require a movement-blocking world block below the hit. An
                    // entity's collider must never become the selected "floor".
                    Vector3 below = point - Vector3.up * 0.05f;
                    BlockValue block = world.GetBlock(Mathf.FloorToInt(below.x),
                        Mathf.FloorToInt(below.y), Mathf.FloorToInt(below.z));
                    if (block.isair || !block.Block.IsCollideMovement) continue;
                    if (hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    target.y = point.y + cfg.GroundClearance;
                    found = true;
                }
                if (!found) return false;
            }

            // Conservative clearance above the drone's placement pivot. The test
            // covers its body, including the sides and overhead space.
            Vector3 halfSize = new Vector3(0.45f, 0.40f, 0.45f);
            Vector3 center = target - origin + Vector3.up * 0.45f;
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, PlacementOverlaps,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= PlacementOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = PlacementOverlaps[i];
                if (collider == null || IsEntityCollider(collider, drone)) continue;
                if (allowOwnerOverlap && IsEntityCollider(collider, owner)) continue;
                return false;
            }
            return true;
        }

        private static bool IsEntityCollider(Collider collider, Entity entity)
        {
            return entity != null && entity.transform != null &&
                (collider.transform == entity.transform || collider.transform.IsChildOf(entity.transform));
        }

        private static Vector3 ComputeBrokenSummonTarget(EntityPlayerLocal owner, ExcuseMeDroneConfig cfg)
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            // Broken drones cannot lift themselves out of an invalid placement.
            // Do not use terrain height here: POI floors/roofs can differ from terrain.
            return owner.position
                + forward * cfg.SummonForwardDistance
                + Vector3.up * cfg.BrokenSummonHeightOffset;
        }

        private static Vector3 ComputeDodgeAnchor(EntityPlayerLocal owner, ExcuseMeDroneConfig cfg)
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 right = owner.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            else
                right.Normalize();

            // One-shot third-person dodge point. After teleport, vanilla drone AI resumes immediately.
            // Negative SideOffset = player-left, positive = player-right.
            return owner.position
                - forward * cfg.BehindDistance
                + right * cfg.SideOffset
                + Vector3.up * cfg.HeightOffset;
        }

        private static void ResetStuckSample(RuntimeState state)
        {
            state.HasSample = false;
            state.StuckAccumulated = 0f;
        }

        private static void DebugLog(string message)
        {
            if (ExcuseMeDroneConfig.Current.DebugLogging)
                Debug.Log("[ExcuseMeDrone] " + message);
        }
    }
}
