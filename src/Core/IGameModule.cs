using System;
using HarmonyLib;

namespace MechanicaMultiplayerFix.Core
{
    /// <summary>
    /// Interface for all game modules that can be loaded/unloaded
    /// Each module handles a specific aspect of the game (Network, Saving, Performance, etc.)
    /// </summary>
    public interface IGameModule
    {
        /// <summary>
        /// Module name for logging and identification
        /// </summary>
        string ModuleName { get; }
        
        /// <summary>
        /// Module version for compatibility tracking
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Is this module enabled via config?
        /// </summary>
        bool IsEnabled { get; set; }
        
        /// <summary>
        /// Initialize the module (called once at mod startup)
        /// Apply Harmony patches here
        /// </summary>
        void Initialize(Harmony harmony);
        
        /// <summary>
        /// Shutdown the module (called when mod is disabled)
        /// Remove patches and cleanup
        /// </summary>
        void Shutdown();
        
        /// <summary>
        /// Called every frame if module needs updates
        /// </summary>
        void Update();
        
        /// <summary>
        /// Module initialization priority (lower = loads first)
        /// Core modules: 0-99
        /// Network modules: 100-199
        /// Saving modules: 200-299
        /// Performance modules: 300-399
        /// UI modules: 400-499
        /// </summary>
        int Priority { get; }
    }
    
    /// <summary>
    /// Base implementation with default behaviors
    /// </summary>
    public abstract class GameModuleBase : IGameModule
    {
        public abstract string ModuleName { get; }
        public virtual string Version => "2.0.0";
        public virtual bool IsEnabled { get; set; } = true;
        public abstract int Priority { get; }
        
        public abstract void Initialize(Harmony harmony);
        
        public virtual void Shutdown()
        {
            // Default: do nothing
        }
        
        public virtual void Update()
        {
            // Default: do nothing
        }
        
        protected void Log(string message)
        {
            UnityEngine.Debug.Log($"[{ModuleName}] {message}");
        }
        
        protected void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[{ModuleName}] {message}");
        }
        
        protected void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[{ModuleName}] {message}");
        }
    }
}
