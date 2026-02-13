using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.oignontom8283.mechanicamultiplayerfix", "MechanicaMultiplayerFix", "1.0.0")]
public class MechanicaMultiplayerFix : BaseUnityPlugin
{
    // Configuration mod entrys
    public static ConfigEntry<bool> enableDebugMode;
    
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

        Debug.Log("Debug mode is " + (enableDebugMode.Value ? "enabled" : "disabled"));
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
        __result = true;
        return false;
    }
}