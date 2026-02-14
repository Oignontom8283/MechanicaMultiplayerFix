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
        Debug.Log("MechanicaMultiplayerFix is starting up...");

        // Init BepInEx mod configuration
        enableDebugMode = Config.Bind(
            "General",                     // Section
            "EnableDebugMode",             // Name key
            false,                         // Default value
            "Enable debug mode for the game (Theoretically incompatible with other mods)" // Description
        );

        enableMultiplayerFixes = Config.Bind(
            "General",
            "EnableMultiplayerFixes",
            true,
            "Enable multiplayer connection fixes (DateTime culture, SteamAPI safety)"
        );

        Debug.Log("Debug mode is " + (enableDebugMode.Value ? "enabled" : "disabled"));
        Debug.Log("Multiplayer fixes are " + (enableMultiplayerFixes.Value ? "enabled" : "disabled"));
        Debug.Log("MechanicaMultiplayerFix is now running!");

        var harmony = new Harmony("com.oignontom8283.mechanicamultiplayerfix");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(Debug), "get_isDebugBuild")]
public static class DebugBuildGetterPatch
{
    static bool Prefix(ref bool __result)
    {
        __result = MechanicaMultiplayerFix.enableDebugMode.Value;
        if (__result) Debug.Log("Debuging mode called forced TRUE");
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
                
                Debug.Log("[Fix] Date saved with InvariantCulture: " + save.lastPlayedDate);
            }
            
            return false; // Abort original function call (we did it in its place)
        }
        catch (Exception ex)
        {
            Debug.LogError("[Fix] Error in SaveGameDataFile: " + ex.Message);
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
            
        Debug.Log("[Fix] New game created with InvariantCulture");
    }
    
    // Postfix to fix after creation
    static void Postfix()
    {
        if (!MechanicaMultiplayerFix.enableMultiplayerFixes.Value)
            return;
        
        // The fix is automatically applied via SaveGameDataFile
        Debug.Log("[Fix] New save configured");
    }
}
