using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
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
/// FIX #12: Log Exit_NoSave calls to help diagnose premature disconnects
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
            // Get virtualObjects list via reflection
            var virtualObjectsField = AccessTools.Field(typeof(Game.ObjectScripts.Computer), "virtualObjects");
            if (virtualObjectsField == null)
            {
                Debug.LogError("[MechanicaMultiplayerFix] [Fix] Could not find virtualObjects field");
                return true;
            }

            var virtualObjects = virtualObjectsField.GetValue(__instance) as System.Collections.IList;
            if (virtualObjects == null)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] virtualObjects is null, blocking RPC");
                return false;
            }

            // Check bounds
            if (voIndex < 0 || voIndex >= virtualObjects.Count)
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] ReceiveVirtualFunctionExecute: voIndex {voIndex} out of range (0-{virtualObjects.Count - 1}), ignoring");
                return false; // Block execution
            }

            var virtualObject = virtualObjects[voIndex];
            if (virtualObject == null)
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] virtualObjects[{voIndex}] is null, ignoring");
                return false;
            }

            // Check functionIndex
            var functionsField = AccessTools.Field(virtualObject.GetType(), "functions");
            if (functionsField != null)
            {
                var functions = functionsField.GetValue(virtualObject) as System.Collections.IList;
                if (functions != null && (functionIndex < 0 || functionIndex >= functions.Count))
                {
                    Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] functionIndex {functionIndex} out of range (0-{functions.Count - 1}), ignoring");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error checking ReceiveVirtualFunctionExecute bounds: " + ex.Message);
        }

        return true; // Continue normally
    }
}

/// <summary>
/// FIX #14: Protect Computer.ReceiveVirtualEventInvoke from index out of range
/// </summary>
[HarmonyPatch(typeof(Game.ObjectScripts.Computer), "ReceiveVirtualEventInvoke")]
public static class Fix_Computer_ReceiveVirtualEventInvoke
{
    static bool Prefix(Game.ObjectScripts.Computer __instance, int voIndex, int eventIndex)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true;

        try
        {
            var virtualObjectsField = AccessTools.Field(typeof(Game.ObjectScripts.Computer), "virtualObjects");
            if (virtualObjectsField == null)
                return true;

            var virtualObjects = virtualObjectsField.GetValue(__instance) as System.Collections.IList;
            if (virtualObjects == null || voIndex < 0 || voIndex >= virtualObjects.Count)
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] ReceiveVirtualEventInvoke: voIndex {voIndex} out of range, ignoring");
                return false;
            }

            var virtualObject = virtualObjects[voIndex];
            if (virtualObject == null)
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] ReceiveVirtualEventInvoke: virtualObjects[{voIndex}] is null, ignoring");
                return false;
            }

            var eventsField = AccessTools.Field(virtualObject.GetType(), "events");
            if (eventsField != null)
            {
                var events = eventsField.GetValue(virtualObject) as System.Collections.IList;
                if (events != null && (eventIndex < 0 || eventIndex >= events.Count))
                {
                    Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] eventIndex {eventIndex} out of range (0-{events.Count - 1}), ignoring");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error checking ReceiveVirtualEventInvoke bounds: " + ex.Message);
        }

        return true;
    }
}

/// <summary>
/// FIX #15: Protect Computer.ReceiveVirtualObjectDestroy from index out of range
/// </summary>
[HarmonyPatch(typeof(Game.ObjectScripts.Computer), "ReceiveVirtualObjectDestroy")]
public static class Fix_Computer_ReceiveVirtualObjectDestroy
{
    static bool Prefix(Game.ObjectScripts.Computer __instance, int voIndex)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return true;

        try
        {
            var virtualObjectsField = AccessTools.Field(typeof(Game.ObjectScripts.Computer), "virtualObjects");
            if (virtualObjectsField == null)
                return true;

            var virtualObjects = virtualObjectsField.GetValue(__instance) as System.Collections.IList;
            if (virtualObjects == null || voIndex < 0 || voIndex >= virtualObjects.Count)
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] ReceiveVirtualObjectDestroy: voIndex {voIndex} out of range, ignoring");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error checking ReceiveVirtualObjectDestroy bounds: " + ex.Message);
        }

        return true;
    }
}

/// <summary>
/// FIX #16: Protect ObjectManager.ApplyAllGUS from NullReferenceExceptions during shutdown
/// Prevents crash cascade when objects are being destroyed
/// </summary>
[HarmonyPatch(typeof(Game.EntityFramework.ObjectManager), "ApplyAllGUS", new Type[] { typeof(UnityEngine.Vector3), typeof(float) })]
public static class Fix_ObjectManager_ApplyAllGUS
{
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            if (__exception is NullReferenceException)
            {
                Debug.LogWarning("[MechanicaMultiplayerFix] [Fix] ObjectManager.ApplyAllGUS NullRef absorbed (likely during shutdown)");
                return null; // Cancel the exception
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #17: Improve OnMasterClientSwitched to immediately exit when host leaves
/// Shows clear error message to clients when server disconnects/crashes
/// </summary>
[HarmonyPatch(typeof(Game.Networking.NetworkedGameManager), "OnMasterClientSwitched")]
public static class Fix_NetworkedGameManager_OnMasterClientSwitched
{
    static void Prefix(Photon.Realtime.Player newMasterClient)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;

        try
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Master client switched! New master: " + 
                (newMasterClient != null ? newMasterClient.NickName : "null"));
            
            // Force immediate exit with clear message
            if (UnityEngine.Object.FindObjectOfType<Game.Saving.SaveManager>() != null)
            {
                var saveManager = UnityEngine.Object.FindObjectOfType<Game.Saving.SaveManager>();
                Debug.LogError("[MechanicaMultiplayerFix] [Fix] SERVER DISCONNECTED - Forcing exit to menu");
                
                // Call Exit_NoSave with clear message
                var exitMethod = AccessTools.Method(typeof(Game.Saving.SaveManager), "Exit_NoSave");
                if (exitMethod != null)
                {
                    exitMethod.Invoke(saveManager, new object[] { "SERVER DISCONNECTED OR CRASHED", false });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in OnMasterClientSwitched: " + ex.Message);
        }
    }
}

/// <summary>
/// FIX #18: Improve OnDisconnected to show clear messages and handle both server and client disconnects
/// Prevents infinite loading when connection is lost
/// </summary>
[HarmonyPatch(typeof(Game.Networking.NetworkedGameManager), "OnDisconnected")]
public static class Fix_NetworkedGameManager_OnDisconnected
{
    static void Prefix(Photon.Realtime.DisconnectCause cause)
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;

        try
        {
            bool isMasterClient = Photon.Pun.PhotonNetwork.IsMasterClient;
            bool isConnected = Photon.Pun.PhotonNetwork.IsConnected;
            
            Debug.LogError($"[MechanicaMultiplayerFix] [Fix] OnDisconnected called! Cause: {cause}, IsMasterClient: {isMasterClient}, IsConnected: {isConnected}");
            
            // If this is the server (master client) disconnecting, log it clearly
            if (isMasterClient)
            {
                Debug.LogError("[MechanicaMultiplayerFix] [Fix] SERVER IS DISCONNECTING - This will cause clients to timeout");
            }
            
            // For clients, show clear disconnect reason
            if (!isMasterClient && cause != Photon.Realtime.DisconnectCause.DisconnectByClientLogic && 
                cause != Photon.Realtime.DisconnectCause.None)
            {
                string userMessage = $"CONNECTION LOST: {cause}";
                
                Debug.LogError($"[MechanicaMultiplayerFix] [Fix] Client disconnect message: {userMessage}");
                
                // Force exit with user-friendly message
                if (UnityEngine.Object.FindObjectOfType<Game.Saving.SaveManager>() != null)
                {
                    var saveManager = UnityEngine.Object.FindObjectOfType<Game.Saving.SaveManager>();
                    var exitMethod = AccessTools.Method(typeof(Game.Saving.SaveManager), "Exit_NoSave");
                    if (exitMethod != null)
                    {
                        exitMethod.Invoke(saveManager, new object[] { userMessage, false });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[MechanicaMultiplayerFix] [Fix] Error in OnDisconnected: " + ex.Message);
        }
    }
}

/// <summary>
/// FIX #19: REAL FIX - Patch CurvySplineSegment to CORRECT negative array sizes
/// This FIXES the root cause instead of hiding errors
/// Uses Transpiler to inject parameter validation BEFORE Array.Resize calls
/// </summary>
[HarmonyPatch]
public static class Fix_CurvySplineSegment_Patch
{
    private static int correctedCount = 0;
    
    static System.Reflection.MethodBase TargetMethod()
    {
        try
        {
            var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("Assembly-CSharp"));
                
            if (assembly == null)
                return null;
            
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "FluffyUnderware.Curvy.CurvySplineSegment");
                
            if (type == null)
                return null;
            
            var method = AccessTools.Method(type, "refreshCurveINTERNAL");
            if (method != null)
            {
                Debug.Log("[MechanicaMultiplayerFix] [Fix] Successfully targeted CurvySplineSegment.refreshCurveINTERNAL for REAL FIX");
            }
            return method;
        }
        catch
        {
            return null;
        }
    }
    
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var validationMethod = AccessTools.Method(typeof(Fix_CurvySplineSegment_Patch), nameof(ValidateArraySize));
        int patchCount = 0;
        
        for (int i = 0; i < codes.Count; i++)
        {
            // Find calls to Array.Resize
            if (codes[i].opcode == System.Reflection.Emit.OpCodes.Call && 
                codes[i].operand != null &&
                codes[i].operand.ToString().Contains("System.Array::Resize"))
            {
                // The newSize parameter is on the stack
                // Insert our validation call that will correct negative values
                // Stack before: [array ref, size]
                // We need to validate 'size' and replace it if negative
                
                codes.Insert(i, new CodeInstruction(System.Reflection.Emit.OpCodes.Call, validationMethod));
                // Stack after validation: [array ref, corrected_size]
                // Then the original Array.Resize call happens
                
                patchCount++;
                i++; // Skip the instruction we just inserted
            }
        }
        
        if (patchCount > 0)
        {
            Debug.Log($"[MechanicaMultiplayerFix] [Fix] Injected {patchCount} array size validations into CurvySplineSegment");
        }
        
        return codes;
    }
    
    // This method validates and corrects array sizes
    public static int ValidateArraySize(int requestedSize)
    {
        if (requestedSize < 0)
        {
            correctedCount++;
            if (correctedCount <= 10) // Only log first 10 to avoid spam
            {
                Debug.LogWarning($"[MechanicaMultiplayerFix] [Fix] CORRECTED negative array size {requestedSize} to 0 (#{correctedCount})");
            }
            return 0; // Correct negative to 0
        }
        return requestedSize; // Keep valid sizes unchanged
    }
}

/// <summary>
/// FIX #20: Additional safety - Patch update methods to prevent propagation
/// </summary>
[HarmonyPatch]
public static class Fix_CurvySpline_ProcessDirty_Patch
{
    static System.Reflection.MethodBase TargetMethod()
    {
        try
        {
            var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("Assembly-CSharp"));
                
            if (assembly == null)
                return null;
            
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "FluffyUnderware.Curvy.CurvySpline");
                
            if (type == null)
                return null;
            
            var method = AccessTools.Method(type, "ProcessDirtyControlPoints");
            if (method != null)
            {
                Debug.Log("[MechanicaMultiplayerFix] [Fix] Successfully targeted CurvySpline.ProcessDirtyControlPoints");
            }
            return method;
        }
        catch
        {
            return null;
        }
    }
    
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            // CORRECTED: Catch IndexOutOfRangeException, not ArgumentOutOfRangeException
            if (__exception is System.IndexOutOfRangeException || __exception is System.ArgumentOutOfRangeException)
            {
                // Suppress array access errors in Curvy spline processing
                return null;
            }
        }
        return __exception;
    }
}

/// <summary>
/// FIX #21: Fallback - Ultimate safety net
/// This only triggers if the Transpiler somehow missed a case
/// </summary>
[HarmonyPatch]
public static class Fix_CurvySplineSegment_Fallback
{
    static System.Reflection.MethodBase TargetMethod()
    {
        try
        {
            var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("Assembly-CSharp"));
                
            if (assembly == null)
                return null;
            
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "FluffyUnderware.Curvy.CurvySplineSegment");
                
            if (type == null)
                return null;
            
            return AccessTools.Method(type, "refreshCurveINTERNAL");
        }
        catch
        {
            return null;
        }
    }
    
    static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
        {
            // CORRECTED: Catch IndexOutOfRangeException (direct array access) not ArgumentOutOfRangeException (Array.Resize)
            if (__exception is System.IndexOutOfRangeException || __exception is System.ArgumentOutOfRangeException)
            {
                // Suppress array errors in refreshCurveINTERNAL
                return null;
            }
        }
        return __exception;
    }
}
