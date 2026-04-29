using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using MechanicaMultiplayerFix.Core;
using Game.Networking;
using Game.Utilities;
using Photon.Pun;

namespace MechanicaMultiplayerFix.Network
{
    /// <summary>
    /// DEFINITIVE SOLUTION to infinite client loading problem
    /// 
    /// CONTEXT:
    /// - ReplicationManager waits for 7 sequential stages to complete
    /// - If A SINGLE RPC is lost → stuck until timeout (120 seconds)
    /// - No retry, no validation
    /// - robotsReplicated never set if no robots → guaranteed timeout
    /// 
    /// SOLUTION:
    /// - Watchdog that monitors progress every 10 seconds
    /// - Forces retry if stuck
    /// - Initializes missing flags
    /// - Clear progress logs for debugging
    /// - Timeout reduced to 45 seconds with automatic retry
    /// </summary>
    public class ReplicationWatchdogModule : GameModuleBase
    {
        public override string ModuleName => "ReplicationWatchdog";
        public override int Priority => 100; // Network modules: 100-199
        
        private Dictionary<string, float> _stageStartTimes = new Dictionary<string, float>();
        private float _lastProgressTime = 0f;
        private bool _watchdogActive = false;
        private Coroutine _watchdogCoroutine = null;
        
        // Configuration
        public float WatchdogInterval { get; set; } = 10f; // Check every 10 seconds
        public float StallTimeout { get; set; } = 30f; // Force retry if no progress for 30s
        public float MaxReplicationTime { get; set; } = 45f; // Total timeout (was 120s)
        
        public override void Initialize(Harmony harmony)
        {
            Log("Initializing replication watchdog system...");
            
            try
            {
                // Patch ReplicationManager.Awake() to start watchdog
                var replicationManagerType = typeof(Game.Networking.ReplicationManager);
                var awakeMethod = AccessTools.Method(replicationManagerType, "Awake");
                
                harmony.Patch(
                    awakeMethod,
                    postfix: new HarmonyMethod(typeof(ReplicationWatchdogModule), nameof(ReplicationManager_Awake_Postfix))
                );
                
                // Patch each flag-setting method to track progress
                PatchFlagMethod(harmony, "SetStorageUnitsReplicated", "StorageUnits");
                PatchFlagMethod(harmony, "SetNaturalResourcesReplicated", "NaturalResources");
                PatchFlagMethod(harmony, "SetRobotsReplicated", "Robots");
                PatchFlagMethod(harmony, "SetObjectsReplicated", "Objects");
                PatchFlagMethod(harmony, "SetPowerLinesReplicated", "PowerLines");
                PatchFlagMethod(harmony, "SetWeldsReplicated", "Welds");
                PatchFlagMethod(harmony, "SetLinksReplicated", "Links");
                
                // Patch ReplicationProcess to use our reduced timeout
                var replicationProcessMethod = AccessTools.Method(replicationManagerType, "ReplicationProcess");
                harmony.Patch(
                    replicationProcessMethod,
                    prefix: new HarmonyMethod(typeof(ReplicationWatchdogModule), nameof(ReplicationProcess_Prefix))
                );
                
                Log("✓ Replication watchdog patches applied");
                Log($"Configuration: Check interval={WatchdogInterval}s, Stall timeout={StallTimeout}s, Max time={MaxReplicationTime}s");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize: {ex.Message}");
            }
        }
        
        private void PatchFlagMethod(Harmony harmony, string methodName, string stageName)
        {
            try
            {
                var method = AccessTools.Method(typeof(ReplicationManager), methodName);
                if (method != null)
                {
                    harmony.Patch(
                        method,
                        postfix: new HarmonyMethod(typeof(ReplicationWatchdogModule), nameof(FlagSet_Postfix))
                    );
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Could not patch {methodName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when ReplicationManager wakes up - start watchdog if client
        /// </summary>
        public static void ReplicationManager_Awake_Postfix(ReplicationManager __instance)
        {
            var module = ModuleLoader.Instance.GetModule<ReplicationWatchdogModule>();
            if (module == null || !module.IsEnabled) return;
            
            // Only start watchdog for clients (not master client)
            if (!PhotonNetwork.IsMasterClient)
            {
                module.Log("Starting replication watchdog for client...");
                module._watchdogActive = true;
                module._lastProgressTime = Time.time;
                
                // Start watchdog coroutine
                if (__instance != null)
                {
                    module._watchdogCoroutine = __instance.StartCoroutine(module.WatchdogCoroutine(__instance));
                }
            }
        }
        
        /// <summary>
        /// Called whenever a replication flag is set - track progress
        /// </summary>
        public static void FlagSet_Postfix()
        {
            var module = ModuleLoader.Instance.GetModule<ReplicationWatchdogModule>();
            if (module == null || !module.IsEnabled) return;
            
            module._lastProgressTime = Time.time;
        }
        
        /// <summary>
        /// Modify ReplicationProcess to use our reduced max replication time
        /// </summary>
        public static void ReplicationProcess_Prefix(ReplicationManager __instance)
        {
            var module = ModuleLoader.Instance.GetModule<ReplicationWatchdogModule>();
            if (module == null || !module.IsEnabled) return;
            
            // Change maxReplicationTime via reflection
            var maxTimeField = AccessTools.Field(typeof(ReplicationManager), "maxReplicationTime");
            if (maxTimeField != null)
            {
                maxTimeField.SetValue(__instance, module.MaxReplicationTime);
            }
        }
        
        /// <summary>
        /// Watchdog coroutine that monitors replication progress
        /// </summary>
        private IEnumerator WatchdogCoroutine(ReplicationManager replicationManager)
        {
            Log("Watchdog started");
            float startTime = Time.time;
            
            while (!replicationManager.fullyReplicated)
            {
                yield return new WaitForSeconds(WatchdogInterval);
                
                float elapsed = Time.time - startTime;
                float sinceProgress = Time.time - _lastProgressTime;
                
                // Check what's replicated so far
                int completed = 0;
                int total = 7;
                
                if (replicationManager.storageUnitsReplicated) completed++;
                if (replicationManager.naturalResourcesReplicated) completed++;
                if (replicationManager.robotsReplicated) completed++;
                if (replicationManager.objectsReplicated) completed++;
                if (replicationManager.powerLinesReplicated) completed++;
                if (replicationManager.weldsReplicated) completed++;
                if (replicationManager.linksReplicated) completed++;
                
                Log($"Replication progress: {completed}/{total} stages ({elapsed:F1}s elapsed)");
                
                // Check for stalls
                if (sinceProgress > StallTimeout)
                {
                    LogWarning($"Replication stalled! No progress for {sinceProgress:F1}s");
                    LogWarning($"Status: Storage={replicationManager.storageUnitsReplicated}, " +
                              $"Resources={replicationManager.naturalResourcesReplicated}, " +
                              $"Robots={replicationManager.robotsReplicated}, " +
                              $"Objects={replicationManager.objectsReplicated}, " +
                              $"Power={replicationManager.powerLinesReplicated}, " +
                              $"Welds={replicationManager.weldsReplicated}, " +
                              $"Links={replicationManager.linksReplicated}");
                    
                    // Try to unstick by forcing requests
                    ForceReplicationRetry(replicationManager);
                    _lastProgressTime = Time.time; // Reset timer
                }
                
                // Ultimate timeout
                if (elapsed > MaxReplicationTime * 1.5f)
                {
                    LogError($"Replication timeout exceeded ({MaxReplicationTime * 1.5f}s)! Forcing completion...");
                    ForceReplicationComplete(replicationManager);
                    break;
                }
            }
            
            Log($"Replication complete after {Time.time - startTime:F1}s");
            _watchdogActive = false;
        }
        
        /// <summary>
        /// Force retry of stuck replication stages
        /// </summary>
        private void ForceReplicationRetry(ReplicationManager replicationManager)
        {
            LogWarning("Forcing replication retry...");
            
            try
            {
                // Robots - if not replicated, assume there are none and set flag
                if (!replicationManager.robotsReplicated)
                {
                    LogWarning("Robots not replicated - assuming none exist, setting flag");
                    replicationManager.robotsReplicated = true;
                }
                
                // Objects - retry request
                if (!replicationManager.objectsReplicated)
                {
                    LogWarning("Objects not replicated - retrying request");
                    if (Singleton<Game.EntityFramework.ObjectManager>.InstanceExists)
                    {
                        Singleton<Game.EntityFramework.ObjectManager>.Instance.StartObjectDataReplication();
                    }
                }
                
                // Power lines - retry
                if (!replicationManager.powerLinesReplicated)
                {
                    LogWarning("Power lines not replicated - retrying request");
                    if (Singleton<Game.Power.PowerManager>.InstanceExists)
                    {
                        Singleton<Game.Power.PowerManager>.Instance.StartPowerLineReplication();
                    }
                }
                
                // Welds - retry
                if (!replicationManager.weldsReplicated)
                {
                    LogWarning("Welds not replicated - retrying request");
                    if (Singleton<Game.Welding.WeldingSystem>.InstanceExists)
                    {
                        Singleton<Game.Welding.WeldingSystem>.Instance.StartWeldsReplication();
                    }
                }
                
                // Links - retry
                if (!replicationManager.linksReplicated)
                {
                    LogWarning("Links not replicated - retrying request");
                    if (Singleton<Game.Programming.LinkManager>.InstanceExists)
                    {
                        Singleton<Game.Programming.LinkManager>.Instance.StartLinkReplication();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error during force retry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Last resort - force mark everything as complete
        /// </summary>
        private void ForceReplicationComplete(ReplicationManager replicationManager)
        {
            LogWarning("FORCING replication complete as last resort!");
            
            replicationManager.storageUnitsReplicated = true;
            replicationManager.naturalResourcesReplicated = true;
            replicationManager.robotsReplicated = true;
            replicationManager.objectsReplicated = true;
            replicationManager.powerLinesReplicated = true;
            replicationManager.weldsReplicated = true;
            replicationManager.linksReplicated = true;
            replicationManager.fullyReplicated = true;
            
            // Hide loading screen
            if (Singleton<Game.UI.LoadingScreen>.InstanceExists)
            {
                Singleton<Game.UI.LoadingScreen>.Instance.Hide();
            }
            
            LogWarning("Game may be partially loaded - some objects might be missing!");
        }
        
        public override void Shutdown()
        {
            _watchdogActive = false;
        }
    }
}
