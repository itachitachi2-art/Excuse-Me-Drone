using HarmonyLib;

namespace Itachi.ExcuseMeDrone
{
    // Suppress new revenge registrations for every drone, regardless of attacker
    // or ownership. Keep native null clears and all non-drone behavior intact.
    // This does not clear existing targets or disable ordinary combat AI.
    [HarmonyPatch(typeof(EntityAlive), "SetRevengeTarget", new System.Type[] { typeof(EntityAlive) })]
    internal static class EntityDroneRevengePatch
    {
        private static bool Prefix(EntityAlive __instance, EntityAlive _other)
        {
            return !(__instance is EntityDrone) || object.ReferenceEquals(_other, null);
        }
    }
}
