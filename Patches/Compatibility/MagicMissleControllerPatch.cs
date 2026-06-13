using HarmonyLib;

namespace BlackMagicAPI.Patches.Compatibility;

/// <summary>
/// Defensive guard for the base-game homing missile.
/// <para>
/// <c>MagicMissleController</c> has no Awake/Start/OnEnable; it is initialized exclusively by
/// <c>SetUp()</c>/<c>AISetup()</c>, which assign <c>playerOwner</c> (and the target/forward vectors).
/// Custom-spell prefabs shipped by third-party mods sometimes carry a leftover
/// <c>MagicMissleController</c> component (cloned from the vanilla missile) that is never
/// <c>SetUp()</c>, so <c>playerOwner</c>/<c>rb</c> stay null and <c>Update()</c> throws a
/// NullReferenceException on every frame.
/// </para>
/// <para>
/// This prefix skips the homing logic until the missile has actually been initialized. Legitimately
/// cast missiles always have <c>playerOwner</c> assigned, so they are unaffected; only stray,
/// uninitialized instances are short-circuited.
/// </para>
/// </summary>
[HarmonyPatch(typeof(MagicMissleController))]
internal class MagicMissleControllerPatch
{
    [HarmonyPatch(nameof(MagicMissleController.Update))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Update_Prefix(MagicMissleController __instance)
    {
        // Run the original Update only once the missile has been initialized.
        return __instance.playerOwner != null && __instance.rb != null;
    }
}
