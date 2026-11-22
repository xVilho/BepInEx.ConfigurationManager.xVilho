using System;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ConfigurationManager
{
    /// <summary>
    /// Gold Mining Simulator: blocks all game input while the config manager window is open.
    /// All gameplay input (walk, look, vehicles, tools, mouse wheel, clicks) goes through the public
    /// InputService query methods, so those return zero/false while the window is open.
    /// GetButtonUp is left alone so releases are never lost.
    /// Types are resolved by name, so nothing is patched in any other game.
    /// </summary>
    internal static class GameLookBlocker
    {
        private static ConfigurationManager _plugin;
        private static ConfigEntry<bool> _enabled;

        private static readonly string[] BlockedMethods =
        {
            "GetAxis1D", "GetAxis2D", "GetButton", "GetButtonDown",
            "TryConsumeToolCycleDown", "TryConsumeVehicleButton", "GetVehicleCameraZoom",
        };

        internal static void TryInstall(ConfigurationManager plugin, ManualLogSource logger)
        {
            var inputService = Type.GetType("InputService, Assembly-CSharp", false);
            if (inputService == null) return;

            _plugin = plugin;
            _enabled = plugin.Config.Bind("General", "Block game input while open", true,
                new ConfigDescription("Block all game input (camera, movement, mouse wheel, clicks, keys) while the config manager window is open."));

            var harmony = new Harmony(ConfigurationManager.GUID + ".inputblocker");
            var patched = 0;
            foreach (var method in inputService.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (Array.IndexOf(BlockedMethods, method.Name) < 0) continue;

                string prefix;
                if (method.ReturnType == typeof(bool)) prefix = nameof(BoolPrefix);
                else if (method.ReturnType == typeof(float)) prefix = nameof(FloatPrefix);
                else if (method.ReturnType == typeof(Vector2)) prefix = nameof(Vector2Prefix);
                else continue;

                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(GameLookBlocker), prefix));
                    patched++;
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to patch InputService." + method.Name + ": " + ex);
                }
            }

            if (patched > 0) logger.LogInfo("Game input is blocked while the window is open (" + patched + " InputService methods)");
            else logger.LogWarning("InputService found but no input methods matched, game input will not be blocked");
        }

        private static bool ShouldBlock => _plugin != null && _plugin.DisplayingWindow && _enabled.Value;

        private static bool BoolPrefix(ref bool __result)
        {
            if (!ShouldBlock) return true;
            __result = false;
            return false;
        }

        private static bool FloatPrefix(ref float __result)
        {
            if (!ShouldBlock) return true;
            __result = 0f;
            return false;
        }

        private static bool Vector2Prefix(ref Vector2 __result)
        {
            if (!ShouldBlock) return true;
            __result = Vector2.zero;
            return false;
        }
    }
}
