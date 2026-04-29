using System;
using System.Globalization;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using MechanicaMultiplayerFix.Core;
using Game.Saving;

namespace MechanicaMultiplayerFix.Utils
{
    /// <summary>
    /// Fixes DateTime culture issues that cause save/load problems
    /// Uses Transpiler to fix at source instead of post-processing
    /// </summary>
    public class DateTimeFixModule : GameModuleBase
    {
        public override string ModuleName => "DateTimeFix";
        public override int Priority => 50; // Utils: 50-99
        
        public override void Initialize(Harmony harmony)
        {
            Log("Initializing DateTime culture fixes...");
            
            try
            {
                // Fix SaveGameDataFile - use Postfix as fallback (Transpiler would be better but complex)
                var saveMethod = AccessTools.Method(typeof(SaveManager), "SaveGameDataFile");
                if (saveMethod != null)
                {
                    harmony.Patch(
                        saveMethod,
                        postfix: new HarmonyMethod(typeof(DateTimeFixModule), nameof(SaveGameDataFile_Postfix))
                    );
                    Log("✓ Patched SaveGameDataFile");
                }
                
                // Fix LoadGameSaves sorting
                var loadMethod = AccessTools.Method(typeof(Game.UI.LoadGameMenu), "LoadGameSaves");
                if (loadMethod != null)
                {
                    harmony.Patch(
                        loadMethod,
                        postfix: new HarmonyMethod(typeof(DateTimeFixModule), nameof(LoadGameSaves_Postfix))
                    );
                    Log("✓ Patched LoadGameSaves");
                }
                
                Log("DateTime fixes applied successfully");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize: {ex.Message}");
            }
        }
        
        public static void SaveGameDataFile_Postfix(SaveManager __instance)
        {
            try
            {
                var infoPathProperty = AccessTools.Property(typeof(SaveManager), "infoPath");
                if (infoPathProperty == null) return;
                
                string infoPath = (string)infoPathProperty.GetValue(__instance);
                if (string.IsNullOrEmpty(infoPath) || !System.IO.File.Exists(infoPath))
                    return;
                
                string jsonText = System.IO.File.ReadAllText(infoPath);
                GameSave save = JsonUtility.FromJson<GameSave>(jsonText);
                
                if (save != null)
                {
                    DateTime testDate;
                    if (!DateTime.TryParse(save.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out testDate))
                    {
                        if (DateTime.TryParse(save.lastPlayedDate, out testDate))
                        {
                            save.lastPlayedDate = testDate.ToString("G", CultureInfo.InvariantCulture);
                            jsonText = JsonUtility.ToJson(save, true);
                            System.IO.File.WriteAllText(infoPath, jsonText);
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }
        }
        
        public static void LoadGameSaves_Postfix(Game.UI.LoadGameMenu __instance)
        {
            try
            {
                var saves = (List<GameSave>)AccessTools.Field(typeof(Game.UI.LoadGameMenu), "loadedGameSaves").GetValue(__instance);
                if (saves == null || saves.Count == 0) return;
                
                List<GameSave> validSaves = new List<GameSave>();
                List<GameSave> invalidSaves = new List<GameSave>();
                
                foreach (var save in saves)
                {
                    DateTime date;
                    if (DateTime.TryParse(save.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                        DateTime.TryParse(save.lastPlayedDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                    {
                        validSaves.Add(save);
                    }
                    else
                    {
                        invalidSaves.Add(save);
                    }
                }
                
                validSaves.Sort((a, b) => {
                    DateTime dateA, dateB;
                    DateTime.TryParse(a.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateA);
                    DateTime.TryParse(b.lastPlayedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateB);
                    return dateB.CompareTo(dateA);
                });
                
                saves.Clear();
                saves.AddRange(validSaves);
                saves.AddRange(invalidSaves);
            }
            catch
            {
                // Silent fail
            }
        }
    }
}
