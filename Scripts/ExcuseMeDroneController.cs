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
            EntityDrone drone = localDrone;
            if (drone == null || drone.Owner != player)
            {
                DebugLog("Summon key pressed, but no local owned drone is currently registered.");
                return;
            }

            // Avoid accidental summons while another modal UI is in use.
            if (LocalPlayerUI.AnyModalWindowOpen()) return;

            float distance = Vector3.Distance(drone.position, player.position);
            if (distance <= cfg.SummonDistance)
            {
                DebugLog("Summon key ignored: drone is already nearby at " + distance.ToString("F1") + "m.");
                return;
            }

            // Keep the user's explicit Stay/Sentry placement intact. F10 is only a
            // rescue/summon shortcut for a drone already ordered to Follow.
            if (!IsFollowOrder(drone))
            {
                DebugLog("Summon denied: drone is " + distance.ToString("F1") + "m away and is not in Follow mode.");
                return;
            }

            RuntimeState runtimeState = GetState(drone);
            bool wasBroken = IsShutdownOrBroken(drone);

            Vector3 anchor = ComputeSummonAnchor(player, cfg);
            Vector3 summonTarget = wasBroken
                ? ComputeBrokenSummonTarget(player, cfg)
                : SnapTeleportTargetToGround(anchor, cfg);
            drone.TeleportToPosition(summonTarget);

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
                DebugLog("Summon: teleported Follow-mode drone from " + distance.ToString("F1") +
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
                Vector3 desired = drone.position;
                Vector3 groundTarget = SnapTeleportTargetToGround(desired, cfg);
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

            Vector3 target = SnapTeleportTargetToGround(ComputeDodgeAnchor(owner, cfg), cfg);
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
            if (ownerDistance >= cfg.StuckDistance && moved <= cfg.StuckMovementThreshold)
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
            Vector3 target = SnapTeleportTargetToGround(ComputeDodgeAnchor(owner, cfg), cfg);
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


        private static Vector3 SnapTeleportTargetToGround(Vector3 desired, ExcuseMeDroneConfig cfg)
        {
            if (!cfg.GroundSnapEnabled)
                return desired;

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
            {
                DebugLog("Ground lookup failed: World is unavailable.");
                return desired;
            }

            float groundY = world.GetHeightAt(desired.x, desired.z);
            if (float.IsNaN(groundY) || float.IsInfinity(groundY))
            {
                DebugLog("Ground lookup failed: invalid height value.");
                return desired;
            }

            Vector3 grounded = desired;
            grounded.y = groundY + cfg.GroundClearance;
            DebugLog("Ground lookup: y=" + groundY.ToString("F2") +
                ", targetY=" + grounded.y.ToString("F2") + ".");
            return grounded;
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
