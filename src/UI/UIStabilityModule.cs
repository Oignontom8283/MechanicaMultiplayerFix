using System;
using HarmonyLib;
using UnityEngine;
using MechanicaMultiplayerFix.Core;
using Game.UI;

namespace MechanicaMultiplayerFix.UI
{
    /// <summary>
    /// Fixes NullReferenceExceptions in UI code (Lobby, PauseMenu, etc.)
    /// Prevents crashes during lobby transitions and menu operations
    /// </summary>
    public class UIStabilityModule : GameModuleBase
    {
        public override string ModuleName => "UIStability";
        public override int Priority => 400; // UI modules: 400-499
        
        public override void Initialize(Harmony harmony)
        {
            Log("Initializing UI stability fixes...");
            
            try
            {
                // Patch lobby UI methods with Finalizers to catch NullRefs
                PatchWithFinalizer(harmony, typeof(Lobby), "UpdateLeaveButton");
                PatchWithFinalizer(harmony, typeof(Lobby), "FullLobbyUIRefresh");
                PatchWithFinalizer(harmony, typeof(Lobby), "PlayerEnterOrLeave");
                PatchWithFinalizer(harmony, typeof(Lobby), "LobbyJoinRequested");
                PatchWithFinalizer(harmony, typeof(Lobby), "OnLobbyEntered");
                
                // Patch PauseMenu.QuitButtonClicked to prevent spurious quits
                var quitMethod = AccessTools.Method(typeof(PauseMenu), "QuitButtonClicked");
                if (quitMethod != null)
                {
                    harmony.Patch(
                        quitMethod,
                        prefix: new HarmonyMethod(typeof(UIStabilityModule), nameof(QuitButtonClicked_Prefix))
                    );
                    Log("✓ Patched PauseMenu.QuitButtonClicked");
                }
                
                Log("UI stability patches applied successfully");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize: {ex.Message}");
            }
        }
        
        private void PatchWithFinalizer(Harmony harmony, Type type, string methodName)
        {
            try
            {
                var method = AccessTools.Method(type, methodName);
                if (method != null)
                {
                    harmony.Patch(
                        method,
                        finalizer: new HarmonyMethod(typeof(UIStabilityModule), nameof(UI_Finalizer))
                    );
                    Log($"✓ Patched {type.Name}.{methodName}");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Could not patch {type.Name}.{methodName}: {ex.Message}");
            }
        }
        
        public static Exception UI_Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            
            var module = ModuleLoader.Instance.GetModule<UIStabilityModule>();
            if (module == null || !module.IsEnabled)
                return __exception;
            
            // Suppress NullReferenceExceptions in UI code
            if (__exception is NullReferenceException)
            {
                // Silently suppress - UI will recover
                return null;
            }
            
            // Suppress format/parsing errors in lobby
            if (__exception is FormatException || __exception is OverflowException)
            {
                return null;
            }
            
            return __exception;
        }
        
        public static bool QuitButtonClicked_Prefix(PauseMenu __instance)
        {
            try
            {
                var isPausedField = AccessTools.Field(typeof(PauseMenu), "isPaused");
                if (isPausedField != null)
                {
                    bool isPaused = (bool)isPausedField.GetValue(__instance);
                    if (!isPaused)
                    {
                        // Block spurious quit when not actually paused
                        return false;
                    }
                }
            }
            catch
            {
                // If we can't check, let it through
            }
            
            return true;
        }
    }
}
