using HarmonyLib;
using System;
using UnityEngine;

namespace BlackMagicAPI.Patches.Compatibility;

/// <summary>
/// Makes the in-game pause/escape menu null-safe.
/// <para>
/// Vanilla <c>MainMenuManager.Update</c> is:
/// <c>if (Input.GetKeyDown(KeyCode.Escape) &amp;&amp; !SpellChooser.instance.isSettingPage) ToggleInGameMenu();</c>
/// After the player finishes the spell-loadout selection, the <c>SpellChooser</c> object can be
/// destroyed, leaving <c>SpellChooser.instance</c> null/destroyed. Dereferencing
/// <c>.isSettingPage</c> then throws every frame, so Escape never opens the pause menu.
/// </para>
/// <para>
/// This prefix reimplements the same behaviour but treats a missing <c>SpellChooser</c> as
/// "not selecting", so Escape always toggles the menu. When the chooser is alive it behaves
/// exactly like vanilla.
/// </para>
/// </summary>
[HarmonyPatch(typeof(MainMenuManager))]
internal class MainMenuEscapePatch
{
    [HarmonyPatch(nameof(MainMenuManager.Update))]
    [HarmonyPrefix]
    private static bool Update_Prefix(MainMenuManager __instance)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var chooser = SpellChooser.instance;
            // Unity's overloaded == treats destroyed objects as null.
            bool settingPage = chooser != null && chooser.isSettingPage;
            BMAPlugin.Log.LogInfo($"[EscapePatch] Escape pressed. settingPage={settingPage}, calling ToggleInGameMenu");
            if (!settingPage)
            {
                try { __instance.ToggleInGameMenu(); }
                catch (System.Exception ex) { BMAPlugin.Log.LogError($"[EscapePatch] ToggleInGameMenu threw: {ex}"); }
            }
        }

        // Update only handled the Escape key; we've fully replaced it.
        return false;
    }

    /// <summary>
    /// Keeps MainMenuManager alive when a third-party plugin's Awake patch throws.
    /// <para>
    /// MageConfigurationAPI (and potentially others) postfix-patch <c>MainMenuManager.Awake</c> and
    /// throw a NullReferenceException because the game's menu UI hierarchy changed
    /// (e.g. <c>Canvas (1)/Main/LobbyID/LobbiesMenu/...</c> no longer exists). When Awake throws,
    /// Unity disables the whole MonoBehaviour, so <c>Update</c> never runs and Escape stops opening the
    /// pause menu. This finalizer swallows the exception so Awake completes and the component stays enabled.
    /// </para>
    /// </summary>
    [HarmonyPatch(nameof(MainMenuManager.Awake))]
    [HarmonyFinalizer]
    private static Exception Awake_Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            BMAPlugin.Log.LogWarning(
                $"Suppressed an exception thrown during MainMenuManager.Awake (likely a third-party " +
                $"menu patch broken by a game update) so the pause menu keeps working: {__exception.Message}");
        }
        return null;
    }
}
