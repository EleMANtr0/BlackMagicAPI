using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BlackMagicAPI.Managers;
using HarmonyLib;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlackMagicAPI;

/// <inheritdoc/>
[BepInProcess("MageArena")]
[BepInDependency(ModMetaData.MOD_SYNC_GUID_DEP, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(ModMetaData.FISH_UTILITIES_GUID_DEP, BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(ModMetaData.GUID, ModMetaData.PLUGIN_NAME, ModMetaData.VERSION)]
public class BMAPlugin : BaseUnityPlugin
{
    [AllowNull]
    internal static BMAPlugin Instance { get; private set; }

    private static Harmony? Harmony;

#pragma warning disable CS8603 // Possible null reference return.
    internal static ManualLogSource Log => Instance._log;
#pragma warning restore CS8603 // Possible null reference return.

    private ManualLogSource? _log;

    /// <inheritdoc/>
    public static string modsync = "all";
    /// <inheritdoc/>
    public static string BlackMagicSyncHash { get; internal set; } = "";

    private void Awake()
    {
        _log = Logger;
        Instance = this;
        Harmony = new(ModMetaData.GUID);
        PatchAllResilient(Harmony);
        Log.LogInfo($"BlackMagicAPI v{ModMetaData.VERSION} loaded, (Compatibility -> v{CompatibilityManager.COMPATIBILITY_VERSION})");
        SynchronizeManager.UpdateSyncHash();
        StartCoroutine(CoWaitForChainloaderToLog());
    }

    /// <summary>
    /// Applies every Harmony patch class in this assembly individually so that a single
    /// patch whose target game method was removed/renamed by an update (e.g. a deleted
    /// PaperInteract.Start) only disables that one feature instead of aborting the whole
    /// plugin load. The original Harmony.PatchAll() is all-or-nothing and throws on the
    /// first undefined target method.
    /// </summary>
    private static void PatchAllResilient(Harmony harmony)
    {
        var assembly = typeof(BMAPlugin).Assembly;
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception ex)
            {
                Log.LogWarning(
                    $"Skipping patch class '{type.FullName}' - its target game method is missing " +
                    $"(likely changed by a game update). This feature is disabled but the mod will " +
                    $"continue loading. Details: {ex.Message}");
            }
        }
    }

    private IEnumerator CoWaitForChainloaderToLog()
    {
        while (!Chainloader._loaded && !SplashScreen.isFinished)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        ModSyncManager.LogAll();
    }
}