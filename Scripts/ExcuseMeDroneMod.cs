using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Itachi.ExcuseMeDrone
{
    public sealed class ExcuseMeDroneMod : IModApi
    {
        public void InitMod(Mod mod)
        {
            Debug.Log("[ExcuseMeDrone] InitMod ENTER v0.1.25. Path=" + (mod != null ? mod.Path : "<null>"));
            try
            {
                ExcuseMeDroneConfig.Load(mod.Path);
                ExcuseMeDroneController.Reset();
                var harmony = new Harmony("itachi.excusemedrone");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log("[ExcuseMeDrone] Harmony PatchAll OK. v0.1.25 loaded.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ExcuseMeDrone] InitMod FAILED: " + ex);
                throw;
            }
        }
    }
}
