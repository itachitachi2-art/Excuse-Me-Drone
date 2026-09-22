using HarmonyLib;

namespace Itachi.ExcuseMeDrone
{
    [HarmonyPatch(typeof(EntityDrone), "OnUpdateEntity")]
    internal static class EntityDroneUpdatePatch
    {
        private static void Prefix(EntityDrone __instance)
        {
            ExcuseMeDroneController.TickBeforeVanilla(__instance);
        }

        private static void Postfix(EntityDrone __instance)
        {
            ExcuseMeDroneController.TickAfterVanilla(__instance);
        }
    }

    // Poll the summon hotkey from the local UI update rather than the drone tick so a
    // short GetKeyDown event is not lost if entity updates are throttled.
    [HarmonyPatch(typeof(LocalPlayerUI), "Update")]
    internal static class LocalPlayerUiUpdatePatch
    {
        private static void Postfix(LocalPlayerUI __instance)
        {
            ExcuseMeDroneController.HandleSummonKey(__instance);
        }
    }
}
