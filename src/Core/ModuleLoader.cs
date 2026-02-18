using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MechanicaMultiplayerFix.Core
{
    /// <summary>
    /// Centralized module loading system
    /// Manages initialization, updates, and shutdown of all game modules
    /// </summary>
    public class ModuleLoader
    {
        private static ModuleLoader _instance;
        public static ModuleLoader Instance => _instance ?? (_instance = new ModuleLoader());
        
        private List<IGameModule> _modules = new List<IGameModule>();
        private bool _initialized = false;
        private Harmony _harmony;
        
        private ModuleLoader()
        {
            // Private constructor for singleton
        }
        
        /// <summary>
        /// Register a module to be loaded
        /// </summary>
        public void RegisterModule(IGameModule module)
        {
            if (_initialized)
            {
                Debug.LogError($"[ModuleLoader] Cannot register module {module.ModuleName} after initialization!");
                return;
            }
            
            _modules.Add(module);
            Debug.Log($"[ModuleLoader] Registered module: {module.ModuleName} (Priority: {module.Priority})");
        }
        
        /// <summary>
        /// Initialize all registered modules in priority order
        /// </summary>
        public void InitializeAll(Harmony harmony)
        {
            if (_initialized)
            {
                Debug.LogWarning("[ModuleLoader] Already initialized!");
                return;
            }
            
            _harmony = harmony;
            
            // Sort by priority (lower priority = loads first)
            _modules = _modules.OrderBy(m => m.Priority).ToList();
            
            Debug.Log($"[ModuleLoader] Initializing {_modules.Count} modules...");
            
            foreach (var module in _modules)
            {
                try
                {
                    if (!module.IsEnabled)
                    {
                        Debug.Log($"[ModuleLoader] Skipping disabled module: {module.ModuleName}");
                        continue;
                    }
                    
                    Debug.Log($"[ModuleLoader] Initializing {module.ModuleName} v{module.Version}...");
                    module.Initialize(_harmony);
                    Debug.Log($"[ModuleLoader] ✓ {module.ModuleName} initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModuleLoader] ✗ Failed to initialize {module.ModuleName}: {ex.Message}");
                    Debug.LogError($"[ModuleLoader] Stack trace: {ex.StackTrace}");
                }
            }
            
            _initialized = true;
            Debug.Log("[ModuleLoader] All modules initialized!");
        }
        
        /// <summary>
        /// Update all modules that need per-frame updates
        /// Call this from a MonoBehaviour Update()
        /// </summary>
        public void UpdateAll()
        {
            if (!_initialized) return;
            
            foreach (var module in _modules)
            {
                try
                {
                    if (module.IsEnabled)
                    {
                        module.Update();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModuleLoader] Error updating {module.ModuleName}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Shutdown all modules
        /// </summary>
        public void ShutdownAll()
        {
            Debug.Log("[ModuleLoader] Shutting down all modules...");
            
            // Shutdown in reverse order
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    Debug.Log($"[ModuleLoader] Shutting down {_modules[i].ModuleName}...");
                    _modules[i].Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ModuleLoader] Error shutting down {_modules[i].ModuleName}: {ex.Message}");
                }
            }
            
            _initialized = false;
            Debug.Log("[ModuleLoader] All modules shut down");
        }
        
        /// <summary>
        /// Get a specific module by type
        /// </summary>
        public T GetModule<T>() where T : IGameModule
        {
            return _modules.OfType<T>().FirstOrDefault();
        }
        
        /// <summary>
        /// Get all loaded modules
        /// </summary>
        public IReadOnlyList<IGameModule> GetAllModules()
        {
            return _modules.AsReadOnly();
        }
    }
}
