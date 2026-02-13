using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.oignontom8283.mechanicamultiplayerfix", "MechanicaMultiplayerFix", "1.0.0")]
public class MechanicaMultiplayerFix : BaseUnityPlugin
{
    void Awake()
    {
        Debug.Log("MechanicaMultiplayerFix is starting up...");

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