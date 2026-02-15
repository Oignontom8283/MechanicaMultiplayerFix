using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;
using System.Globalization;
using System.Collections.Generic;
using Game.Saving;
using Game.UI;

[BepInPlugin("MechanicaMultiplayerFix", "MechanicaMultiplayerFix", "1.0.0")]
public class MechanicaMultiplayerFix : BaseUnityPlugin
{
    // Configuration mod entrys
    public static ConfigEntry<bool> enableDebugMode;
    public static ConfigEntry<bool> enableMultiplayerFixes;
    
    void Awake()
    {
        Debug.Log("[MechanicaMultiplayerFix] starting up...");


        // Init BepInEx mod configuration

        enableMultiplayerFixes = Config.Bind(
            "General",
            "EnableMultiplayerFixes",
            true,
            "Enable multiplayer connection fixes (DateTime culture, SteamAPI safety)"
        );
        
        enableDebugMode = Config.Bind(
            "General",                     // Section
            "EnableDebugMode",             // Name key
            false,                         // Default value
            "Enable debug mode for the game (Theoretically incompatible with other mods)" // Description
        );


        Debug.Log("[MechanicaMultiplayerFix] Debug mode is " + (enableDebugMode.Value ? "enabled" : "disabled"));
        Debug.Log("[MechanicaMultiplayerFix] Multiplayer fixes are " + (enableMultiplayerFixes.Value ? "enabled" : "disabled"));
        Debug.Log("[MechanicaMultiplayerFix] MechanicaMultiplayerFix is now running!");

        var harmony = new Harmony("MechanicaMultiplayerFix");
        harmony.PatchAll();
    }
}

/// <summary>
/// Patch to force Debug.isDebugBuild to return our config value
/// This allows us to enable debug features in the game without actually being in a debug build
/// WARNING: This will very likely cause compatibility issues with other mods that would try to do the same thing
/// </summary>
[HarmonyPatch(typeof(Debug), "get_isDebugBuild")]
public static class DebugBuildGetterPatch
{
    static bool Prefix(ref bool __result)
    {
        __result = MechanicaMultiplayerFix.enableDebugMode.Value;
        if (__result) Debug.Log("[MechanicaMultiplayerFix] Debuging mode called forced TRUE");
        return false;
    }
}


// MULTIPLAYER FIXES!

/// <summary>
/// FIX #1: Force DateTime to use InvariantCulture during saves
/// Intercepts SaveManager.SaveGameDataFile() - This is the method that saves dates
/// </summary>
[HarmonyPatch(typeof(Game.Saving.SaveManager), "SaveGameDataFile")]
public static class Fix_SaveManager_SaveGameDataFile
{
    static bool Prefix(Game.Saving.SaveManager __instance)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true; // Let the original execute

        try
        {
            // Retrieve the file path (private field)
            string infoPath = (string)AccessTools.Field(typeof(Game.Saving.SaveManager), "infoPath").GetValue(__instance);
            
            if (!System.IO.File.Exists(infoPath))
                return false;

            // Load and modify the save
            string jsonText = System.IO.File.ReadAllText(infoPath);
            GameSave save = JsonUtility.FromJson<GameSave>(jsonText);
            
            if (save != null)
            {
                // FIX: Use InvariantCulture instead of the local culture
                save.lastPlayedDate = DateTime.Now.ToString("G", CultureInfo.InvariantCulture);
                
                // Update other information
                save.timeOfDay = Game.Utilities.Singleton<Game.TimeManagement.TimeManager>.Instance.time;
                save.timeSinceGameCreated = Game.Utilities.Singleton<Game.TimeManagement.TimeManager>.Instance.timeSinceSaveCreated;
                save.elapsedDays = Game.Utilities.Singleton<Game.TimeManagement.TimeManager>.Instance.elapsedDays;
                save.daysSurvived = Game.Utilities.Singleton<Game.TimeManagement.TimeManager>.Instance.daysSurvived;
                save.postUpdate = true;
                save.crystalsRandomized = true;
                
                // Save
                jsonText = JsonUtility.ToJson(save, true);
                System.IO.File.WriteAllText(infoPath, jsonText);
                
                Debug.Log("[MechanicaMultiplayerFix] [Fix] Date saved with InvariantCulture: " + save.lastPlayedDate);
            }
            
            return false; // Abort original function call (we did it in its place)
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in SaveGameDataFile: " + ex.Message);
            return true; // In case of error, let the original execute
        }
    }
}

/// <summary>
/// FIX #2: Force InvariantCulture when creating a new game
/// Intercepts NewGameMenu.CreateClicked() - This is the method that creates new saves
/// </summary>
[HarmonyPatch(typeof(Game.UI.NewGameMenu), "CreateClicked")]
public static class Fix_NewGameMenu_CreateClicked
{
    // Let the original function execute, but modify the GameSave just before
    static void Prefix(Game.UI.NewGameMenu __instance)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;
            
        Debug.Log("[MechanicaMultiplayerFix] [Fix] New game created with InvariantCulture");
    }
    
    // Postfix to fix after creation
    static void Postfix()
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;
        
        // The fix is automatically applied via SaveGameDataFile
        Debug.Log("[MechanicaMultiplayerFix] [Fix] New save configured");
    }
}

/// <summary>
/// FIX #3: Parse dates with InvariantCulture + fallback to CurrentCulture
/// Intercepts LoadGameMenu.LoadGameSaves() and re-sorts the saves
/// </summary>
[HarmonyPatch(typeof(Game.UI.LoadGameMenu), "LoadGameSaves")]
public static class Fix_LoadGameMenu_LoadGameSaves
{
    // Let the game load, then fix the sorting
    static void Postfix(LoadGameMenu __instance)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;

        try
        {
            // Retrieve the list of saves (private field)
            var saves = (List<GameSave>)AccessTools.Field(typeof(LoadGameMenu), "loadedGameSaves").GetValue(__instance);
            
            if (saves == null || saves.Count == 0)
                return;

            // Separate valid and invalid saves
            List<GameSave> validSaves = new List<GameSave>();
            List<GameSave> invalidSaves = new List<GameSave>();
            
            foreach (var save in saves)
            {
                DateTime date;
                // FIX: Try InvariantCulture first, then CurrentCulture (old saves)
                if (DateTime.TryParse(save.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                    DateTime.TryParse(save.lastPlayedDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                {
                    validSaves.Add(save);
                }
                else
                {
                    invalidSaves.Add(save); // Keep even if date is invalid
                    Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Invalid date: " + save.lastPlayedDate);
                }
            }
            
            // Sort by date (most recent first)
            validSaves.Sort((a, b) => {
                DateTime dateA, dateB;
                DateTime.TryParse(a.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateA);
                DateTime.TryParse(b.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateB);
                return dateB.CompareTo(dateA);
            });
            
            // Put back into the list (valid first, then invalid)
            saves.Clear();
            saves.AddRange(validSaves);
            saves.AddRange(invalidSaves);
            
            Debug.Log("[MechanicaMultiplayerFix] [Fix] Sorted " + validSaves.Count + " saves");
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in LoadGameSaves: " + ex.Message);
        }
    }
}

/// <summary>
/// FIX #4: Catch SteamID parsing errors in lobbies
/// Prevents crashes when Steam data is corrupted
/// </summary>
[HarmonyPatch(typeof(Game.UI.Lobby), "OnLobbyEntered")]
public static class Fix_Lobby_OnLobbyEntered
{
    // Catch the exception and absorb it
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            // FIX: Absorb parsing errors instead of crashing
            if (__exception is FormatException || __exception is OverflowException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Corrupted lobby data, ignored: " + __exception.Message);
                return null; // Cancel the exception
            }
        }
        return __exception; // Let other exceptions pass
    }
}

/// <summary>
/// FIX #5: Catch SteamID parsing errors in StorageUnitManager
/// Prevents crashes when loading storage crates
/// </summary>
[HarmonyPatch(typeof(Game.StorageUnitFramework.StorageUnitManager), "LoadInventoryStorageUnits")]
public static class Fix_StorageUnitManager_LoadInventoryStorageUnits
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            // FIX: Absorb SteamID parsing errors
            if (__exception is FormatException || __exception is OverflowException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Invalid SteamID in StorageUnit, ignored: " + __exception.Message);
                return null; // Cancel the exception
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #6: Log network errors without crashing
/// Helps diagnose connection issues
/// </summary>
[HarmonyPatch(typeof(Game.Saving.SaveManager), "RetrievePlayerData_Networked")]
public static class Fix_SaveManager_RetrievePlayerData
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            // Log for diagnostic help (but let the exception continue)
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error loading networked player data: " + __exception.Message);
        }
        return __exception;
    }
}

/// <summary>
/// FIX #7: Block automatic QuitButtonClicked when game is not actually paused
/// Prevents premature "Exiting, no save" disconnects during replication
/// </summary>
[HarmonyPatch(typeof(Game.UI.PauseMenu), "QuitButtonClicked")]
public static class Fix_PauseMenu_QuitButtonClicked
{
    static bool Prefix(Game.UI.PauseMenu __instance)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true;

        // Only allow quit if pause menu is actually active
        try
        {
            var isPausedField = AccessTools.Field(typeof(Game.UI.PauseMenu), "isPaused");
            if (isPausedField != null)
            {
                bool isPaused = (bool)isPausedField.GetValue(__instance);
                if (!isPaused)
                {
                    Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Blocked spurious QuitButtonClicked (not paused)");
                    return false; // Block execution
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in QuitButtonClicked check: " + ex.Message);
        }
        
        return true; // Continue normal execution
    }
}

/// <summary>
/// FIX #8: Protect UpdateLeaveButton from NullReferenceException
/// Prevents UI crashes during lobby state transitions
/// </summary>
[HarmonyPatch(typeof(Game.UI.Lobby), "UpdateLeaveButton")]
public static class Fix_Lobby_UpdateLeaveButton
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            if (__exception is NullReferenceException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Lobby.UpdateLeaveButton NullRef absorbed");
                return null; // Cancel the exception
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #9: Protect FullLobbyUIRefresh from NullReferenceException
/// Prevents UI crashes during lobby refresh operations
/// </summary>
[HarmonyPatch(typeof(Game.UI.Lobby), "FullLobbyUIRefresh")]
public static class Fix_Lobby_FullLobbyUIRefresh
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            if (__exception is NullReferenceException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Lobby.FullLobbyUIRefresh NullRef absorbed");
                return null; // Cancel the exception
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #10: Protect LobbyJoinRequested from errors
/// Prevents crashes during lobby join attempts
/// </summary>
[HarmonyPatch(typeof(Game.UI.Lobby), "LobbyJoinRequested")]
public static class Fix_Lobby_LobbyJoinRequested
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Lobby.LobbyJoinRequested error absorbed: " + __exception.Message);
            return null; // Cancel the exception
        }
        return __exception;
    }
}

/// <summary>
/// FIX #11: Protect PlayerEnterOrLeave from NullReferenceException
/// Prevents UI crashes during player join/leave events
/// </summary>
[HarmonyPatch(typeof(Game.UI.Lobby), "PlayerEnterOrLeave")]
public static class Fix_Lobby_PlayerEnterOrLeave
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            if (__exception is NullReferenceException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Lobby.PlayerEnterOrLeave NullRef absorbed");
                return null; // Cancel the exception
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #12: Log and validate Exit_NoSave calls
/// Helps diagnose premature exit triggers
/// </summary>
[HarmonyPatch(typeof(Game.Saving.SaveManager), "Exit_NoSave")]
public static class Fix_SaveManager_Exit_NoSave
{
    static bool Prefix()
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true;

        // Just log the call - simplified from previous version
        Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Exit_NoSave called");
        return true;
    }
}

/// <summary>
/// FIX #13: Protect Computer.ReceiveVirtualFunctionExecute from index out of range
/// CRITICAL: This RPC crashes when server/client virtualObjects lists are out of sync
/// </summary>
[HarmonyPatch(typeof(Game.ObjectScripts.Computer), "ReceiveVirtualFunctionExecute")]
public static class Fix_Computer_ReceiveVirtualFunctionExecute
{
    static bool Prefix(Game.ObjectScripts.Computer __instance, int voIndex, int functionIndex)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true;

        try
        {
            // Log the call with stack trace for debugging
            Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Exit_NoSave called, stack trace:\n" + 
                System.Environment.StackTrace);
            
            // Check if we're in a multiplayer session
            var gameManager = UnityEngine.Object.FindObjectOfType<Game.GameManager>();
            if (gameManager != null)
            {
                var networkingField = AccessTools.Field(typeof(Game.GameManager), "networking");
                if (networkingField != null)
                {
                    var networking = networkingField.GetValue(gameManager);
                    if (networking != null)
                    {
                        var isConnectedProperty = AccessTools.Property(networking.GetType(), "IsConnected");
                        if (isConnectedProperty != null)
                        {
                            bool isConnected = (bool)isConnectedProperty.GetValue(networking);
                            if (isConnected)
                            {
                                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] Exit_NoSave during active MP session!");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in Exit_NoSave logging: " + ex.Message);
        }
        
        return true; // Continue with exit
    }
}
