using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGuid, PluginName, ModVersion)]
public sealed class MadOutLegacyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.alonso.madoutlegacy";
    public const string PluginName = "MadOutLegacy";
    public const string ModVersion = "1.2.1";
    public const string ModVersionName = "1.2.1";

    internal static ManualLogSource Log;
    internal static Harmony Harmony;

    private void Awake()
    {
        Log = Logger;
        LegacyCompatibility.DetectFromRuntime();

        Harmony = new Harmony(PluginGuid);
        try
        {
            if (LegacyCompatibility.IsFullMode)
            {
                LegacyCommandLine.UpdateFromArgs();
                LegacyServerConsole.PrepareForServerMode();
                LegacyDebugOverlay.InitIfNeeded();
            }

            LegacyPatchManager.PatchForCurrentMode(Harmony, Logger);

            if (LegacyCompatibility.IsFullMode)
            {
                Logger.LogInfo(PluginName + " " + ModVersionName + " loaded in full mode for game version " + LegacyCompatibility.CurrentGameVersion);
            }
            else
            {
                Logger.LogWarning(PluginName + " " + ModVersionName + " loaded in compatibility mode for game version " + LegacyCompatibility.CurrentGameVersion + ". Only offline patches are enabled.");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError(PluginName + " Harmony patching failed: " + ex);
        }
    }

    private void Update()
    {
        LegacyHotkeys.Update();
    }

    private void OnDestroy()
    {
        if (Harmony != null)
        {
            Harmony.UnpatchSelf();
            Harmony = null;
        }
    }
}
