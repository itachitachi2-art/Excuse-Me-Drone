using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Itachi.ExcuseMeDrone
{
    internal sealed class ExcuseMeDroneConfig
    {
        internal static ExcuseMeDroneConfig Current { get; private set; }

        static ExcuseMeDroneConfig()
        {
            Current = new ExcuseMeDroneConfig();
        }

        internal KeyCode SummonKey = KeyCode.F10;
        internal float SummonDistance = 5.0f;
        internal float SummonForwardDistance = 1.50f;
        internal float SummonHeightOffset = 1.20f;
        internal bool GroundSnapEnabled = true;
        internal float GroundClearance = 0.10f;
        internal float BrokenSummonHeightOffset = 0.80f;
        internal float BrokenLiftThreshold = 0.25f;
        internal float BrokenMinimumAwakeSeconds = 1.50f;
        internal float BrokenReviveTimeoutSeconds = 6.00f;

        internal bool CombatDodgeEnabled = true;
        internal float CombatTriggerDistance = 5.0f;
        internal float CombatDodgeCooldownSeconds = 60.0f;
        internal float ThreatScanInterval = 0.25f;
        internal float BehindDistance = 0.60f;
        internal float SideOffset = -1.80f;
        internal float HeightOffset = 1.10f;

        internal bool StuckRescueEnabled = true;
        internal float StuckDistance = 8.0f;
        internal float StuckSeconds = 3.0f;
        internal float StuckMovementThreshold = 0.20f;
        internal float ImmediateRescueDistance = 20.0f;

        internal bool Invisible = false;
        internal bool DebugLogging = true;

        internal static void Load(string modPath)
        {
            var config = new ExcuseMeDroneConfig();
            string path = Path.Combine(modPath, "Config", "ExcuseMeDrone.cfg");

            try
            {
                if (!File.Exists(path))
                {
                    Current = config;
                    Debug.LogWarning("[ExcuseMeDrone] Config not found; using built-in defaults: " + path);
                    return;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }

                // New names first; old v0.1.12 Menu* names are accepted for config compatibility.
                config.SummonKey = ParseKey(values, "SummonKey", ParseKey(values, "MenuKey", config.SummonKey));
                config.SummonDistance = ParseFloat(values, "SummonDistance",
                    ParseFloat(values, "MenuRescueDistance", config.SummonDistance, 0f, 50f), 0f, 50f);
                config.SummonForwardDistance = ParseFloat(values, "SummonForwardDistance",
                    ParseFloat(values, "MenuTeleportForwardDistance", config.SummonForwardDistance, 0.5f, 5f), 0.5f, 5f);
                config.SummonHeightOffset = ParseFloat(values, "SummonHeightOffset",
                    ParseFloat(values, "MenuTeleportHeightOffset", config.SummonHeightOffset, 0.2f, 4f), 0.2f, 4f);
                config.GroundSnapEnabled = ParseBool(values, "GroundSnapEnabled",
                    ParseBool(values, "SummonGroundSnapEnabled", config.GroundSnapEnabled));
                config.GroundClearance = ParseFloat(values, "GroundClearance",
                    ParseFloat(values, "SummonGroundClearance", config.GroundClearance, 0f, 1f), 0f, 1f);
                config.BrokenSummonHeightOffset = ParseFloat(values, "BrokenSummonHeightOffset",
                    ParseFloat(values, "BrokenGroundClearance", config.BrokenSummonHeightOffset, 0.30f, 3f), 0.30f, 3f);
                config.BrokenLiftThreshold = ParseFloat(values, "BrokenLiftThreshold", config.BrokenLiftThreshold, 0.05f, 1f);
                config.BrokenMinimumAwakeSeconds = ParseFloat(values, "BrokenMinimumAwakeSeconds", config.BrokenMinimumAwakeSeconds, 0.10f, 10f);
                config.BrokenReviveTimeoutSeconds = ParseFloat(values, "BrokenReviveTimeoutSeconds", config.BrokenReviveTimeoutSeconds, config.BrokenMinimumAwakeSeconds, 15f);

                config.CombatDodgeEnabled = ParseBool(values, "CombatDodgeEnabled", config.CombatDodgeEnabled);
                config.CombatTriggerDistance = ParseFloat(values, "CombatTriggerDistance", config.CombatTriggerDistance, 1f, 50f);
                config.CombatDodgeCooldownSeconds = ParseFloat(values, "CombatDodgeCooldownSeconds", config.CombatDodgeCooldownSeconds, 0f, 600f);
                config.ThreatScanInterval = ParseFloat(values, "ThreatScanInterval", config.ThreatScanInterval, 0.05f, 5f);
                config.BehindDistance = ParseFloat(values, "BehindDistance", config.BehindDistance, 0.0f, 5f);
                config.SideOffset = ParseFloat(values, "SideOffset", config.SideOffset, -5f, 5f);
                config.HeightOffset = ParseFloat(values, "HeightOffset", config.HeightOffset, 0.2f, 4f);

                config.StuckRescueEnabled = ParseBool(values, "StuckRescueEnabled", config.StuckRescueEnabled);
                config.StuckDistance = ParseFloat(values, "StuckDistance", config.StuckDistance, 2f, 50f);
                config.StuckSeconds = ParseFloat(values, "StuckSeconds", config.StuckSeconds, 1f, 30f);
                config.StuckMovementThreshold = ParseFloat(values, "StuckMovementThreshold", config.StuckMovementThreshold, 0.01f, 5f);
                config.ImmediateRescueDistance = ParseFloat(values, "ImmediateRescueDistance", config.ImmediateRescueDistance, config.StuckDistance, 100f);

                config.Invisible = ParseBool(values, "Invisible", config.Invisible);
                config.DebugLogging = ParseBool(values, "DebugLogging", config.DebugLogging);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ExcuseMeDrone] Failed to read config; using parsed/default values. " + ex);
            }

            Current = config;
            Debug.Log("[ExcuseMeDrone] Config loaded. SummonKey=" + config.SummonKey + ", Invisible=" + config.Invisible);
        }

        private static bool ParseBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string raw;
            bool value;
            return values.TryGetValue(key, out raw) && bool.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(Dictionary<string, string> values, string key, float fallback, float min, float max)
        {
            string raw;
            float value;
            if (!values.TryGetValue(key, out raw) ||
                !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static KeyCode ParseKey(Dictionary<string, string> values, string key, KeyCode fallback)
        {
            string raw;
            KeyCode value;
            return values.TryGetValue(key, out raw) && Enum.TryParse(raw, true, out value) ? value : fallback;
        }
    }
}
