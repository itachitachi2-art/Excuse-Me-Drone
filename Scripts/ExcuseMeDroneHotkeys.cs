using UnityEngine;
using HarmonyLib;

namespace Itachi.ExcuseMeDrone
{
    // Session-only lock. Never serialize this mode or replace the drone's inventory.
    internal static class ExcuseMeDroneHotkeys
    {
        private static EntityDrone locked;
        private static int lastFrame = -1;
        private static bool combatSuspended;
        private static float lastCombatTime;
        internal const float CombatReturnDelay = 2f;
        internal static void Reset() { locked = null; combatSuspended = false; lastCombatTime = 0f; lastFrame = -1; }
        internal static bool IsLockRequested(EntityDrone drone)
        {
            if (locked != drone || drone == null) return false;
            EntityPlayerLocal owner = drone.Owner as EntityPlayerLocal;
            if (owner == null || owner.IsDead() || drone.Health <= 1 || (int)drone.GetState() == 5 ||
                GameManager.Instance == null || GameManager.Instance.World == null ||
                GameManager.Instance.World.GetEntity(drone.entityId) != drone)
            { locked = null; combatSuspended = false; return false; }
            return true;
        }
        // Only native stun combat suspends positioning. A heal target is also stored in
        // GetAttackTarget(), so that API must never be treated as proof of an enemy.
        internal static bool IsLocked(EntityDrone drone)
        {
            if (!IsLockRequested(drone)) return false;
            bool baton = false;
            var weapons = drone.GetInstalledWeapons();
            if (weapons != null)
                foreach (var weapon in weapons)
                    if (weapon is DroneWeapons.StunBeamWeapon) { baton = true; break; }
            bool attacking = baton && drone.activeWeapon is DroneWeapons.StunBeamWeapon &&
                ((int)drone.GetState() == 4 || (int)drone.transitionState == 4);
            if (attacking)
            {
                combatSuspended = true;
                lastCombatTime = Time.time;
                return false;
            }
            if (!combatSuspended) return true;
            // Finish the native retreat and any immediately following heal before
            // snapping back. This also bridges gaps between consecutive stun tasks.
            if ((int)drone.GetState() == 3 || (int)drone.transitionState == 3 ||
                Time.time - lastCombatTime < CombatReturnDelay) return false;
            combatSuspended = false;
            return true;
        }

        internal static void ReleaseWithoutSummon(EntityDrone drone)
        { if (locked == drone) { locked = null; combatSuspended = false; } }

        private static bool Pressed(KeyCode key)
        {
            if (Input.GetKeyDown(key)) return true;
            // JIS punctuation is not mapped consistently to KeyCode.At/Colon by Unity.
            // The character fallback is used only after modal/input guards below.
            return Input.anyKeyDown && (key == KeyCode.At && Input.inputString == "@" ||
                key == KeyCode.Colon && Input.inputString == ":");
        }

        private static EntityDrone FindOwned(EntityPlayerLocal player)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null || DroneManager.Instance == null) return null;
            if (locked != null && locked.Owner == player && world.GetEntity(locked.entityId) == locked)
                return locked;
            foreach (EntityCreationData saved in DroneManager.Instance.GetAllDronesECD())
            {
                if (saved.belongsPlayerId != player.entityId) continue;
                EntityDrone drone = world.GetEntity(saved.id) as EntityDrone;
                if (drone != null && drone.Owner == player) return drone;
            }
            return null;
        }

        internal static void Handle(LocalPlayerUI ui)
        {
            if (ui == null || ui.entityPlayer == null || ui.entityPlayer.IsDead() || ui.xui == null ||
                ui.windowManager == null || ui.windowManager.IsUGUIInputActive()) return;
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var cfg = ExcuseMeDroneConfig.Current;
            bool inventoryPressed = Pressed(cfg.InventoryKey);
            bool behindPressed = Pressed(cfg.BehindLockKey);
            if (!inventoryPressed && !behindPressed) return;
            EntityDrone drone = FindOwned(ui.entityPlayer);
            if (drone == null) return;
            var group = ui.windowManager.GetWindow(XUiC_BagStorageWindowGroup.ID) as XUiWindowGroup;
            var storage = group != null ? group.Controller as XUiC_BagStorageWindowGroup : null;
            bool ours = storage != null && storage.Entity == drone && ui.windowManager.IsWindowOpen(group);
            GUIWindow modal = ui.windowManager.GetModalWindow();
            if (modal != null && (!ours || modal != group)) return;
            if (inventoryPressed)
            {
                if (ours) ui.windowManager.Close(group, false);
                else
                {
                    float max = Constants.cDigAndBuildDistance + 1f;
                    if ((drone.position - ui.entityPlayer.position).sqrMagnitude <= max * max)
                        drone.OnEntityActivated(new EntityActivationCommand("storage", "", "", ""), ui.entityPlayer);
                }
                return;
            }
            if (modal != null || !behindPressed) return;
            // Continuous positioning must be authoritative, not a client-only visual trick.
            if (ConnectionManager.Instance == null || !ConnectionManager.Instance.IsServer)
            { Debug.LogWarning("[ExcuseMeDrone] Behind lock currently requires single-player/local host."); return; }
            if (IsLockRequested(drone))
            {
                ReleaseWithoutSummon(drone);
                drone.FollowMode();
                ExcuseMeDroneController.SummonDrone(drone, ui.entityPlayer, cfg);
            }
            else if (ExcuseMeDroneController.CanLock(drone))
            {
                locked = drone;
                combatSuspended = false;
                Debug.Log("[ExcuseMeDrone] Behind lock enabled.");
                UpdateLocked(drone);
            }
        }

        internal static Vector3 BehindTarget(Vector3 position, Vector3 forward, float distance, float height)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            else forward.Normalize();
            return position - forward * distance + Vector3.up * height;
        }

        internal static void UpdateLocked(EntityDrone drone)
        {
            if (!IsLocked(drone)) return;
            var owner = (EntityPlayerLocal)drone.Owner;
            var cfg = ExcuseMeDroneConfig.Current;
            drone.motion = Vector3.zero;
            drone.clearCurrentPath();
            Vector3 desired = BehindTarget(owner.position, owner.transform.forward, cfg.BehindLockDistance, cfg.HeightOffset);
            Vector3 target;
            // Do not put the drone into geometry or teleport it through a wall.
            Vector3 from = owner.position + Vector3.up * cfg.HeightOffset;
            Vector3 delta = desired - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from - Origin.position, delta.normalized,
                delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && !ExcuseMeDroneController.IsEntityCollider(hit.collider, owner) &&
                    !ExcuseMeDroneController.IsEntityCollider(hit.collider, drone)) return;
            if (ExcuseMeDroneController.TryPlacement(owner, desired, drone, cfg, false, false, out target))
                drone.SetPosition(target, true);
        }
    }

    [HarmonyPatch(typeof(EntityDrone), "updateState")]
    internal static class DroneLockedStatePatch
    {
        private static bool Prefix(EntityDrone __instance)
        { return !ExcuseMeDroneHotkeys.IsLocked(__instance); }
    }
}
