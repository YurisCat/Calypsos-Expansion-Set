using System.Reflection;
using EnhancedEditor;
using EnhancedFramework.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using _200_Dev.Entities;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;
using _200_Dev.UI.Networking;
using _200_Dev.BoardGameLogic.Elements.Tool.ToolExtension;

namespace CalypsoExpansionSet.Testing;

// Every patch is inert unless both debug settings and the dedicated launch argument are present.
[HarmonyPatch(typeof(SplashManager), "OnPlay")]
internal static class TestSkipLogosPatch
{
    private static void Prefix(SplashManager __instance)
    {
        if (!TestRoomLaunch.IsActive) return;
        // Keep the native next-scene load, splash state removal and bundle cleanup.
        __instance.animations = new Il2CppReferenceArray<PolymorphValue<SplashAnimation>>(0);
        CalypsoExpansionSetPlugin.Instance!.Log.LogInfo("[Test room] splash animation list skipped; native initialization continues.");
    }
}

[HarmonyPatch]
internal static class TestNoSaveWritesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Save), nameof(Save.AutoSave));
        yield return AccessTools.Method(typeof(Save), nameof(Save.SrvSaveCollection));
        yield return AccessTools.Method(typeof(Save), "SaveToSlot");
    }
    private static bool Prefix() => !TestRoomLaunch.IsActive;
}

[HarmonyPatch(typeof(VhsNetworkManager), nameof(VhsNetworkManager.OnServerConnect))]
internal static class TestLocalConnectionOnlyPatch
{
    private static bool Prefix(NetworkConnectionToClient conn)
    {
        if (!TestRoomLaunch.IsActive || conn.TryCast<LocalConnectionToClient>() != null) return true;
        CalypsoExpansionSetPlugin.Instance!.Log.LogWarning("[Test room] remote connection refused: this procedural gallery is single-player.");
        conn.Disconnect();
        return false;
    }
}

[HarmonyPatch]
internal static class TestNoEventsPatch
{
    // Avoid the IL2CPP Nullable<int> argument of SrvBeginInternal.
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(GameEventManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(GameEventManager.SrvBegin));
    private static bool Prefix() => !TestRoomLaunch.IsActive;
}

[HarmonyPatch]
internal static class TestNoMonsterSpawnsPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(GameEventManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name is "SrvMonsterSpawn" or "SrvMonsterSpawnNoDelay");
    private static bool Prefix() => !TestRoomLaunch.IsActive;
}

[HarmonyPatch(typeof(EnvironmentElements.ZoneManager), nameof(EnvironmentElements.ZoneManager.SrvDamageAllPlayersNotInZone))]
internal static class TestNoRoundBoundaryDamagePatch
{
    private static bool Prefix() => !TestRoomLaunch.IsActive;
}

[HarmonyPatch(typeof(UIManager), nameof(UIManager.CliShowGlobalMessage))]
internal static class TestHideOnlyJoinWarningPatch
{
    private static bool Prefix(string message)
    {
        if (!TestRoomLaunch.IsActive) return true;
        var translated = UIManager.LocalizeIfTerm(TestRoomMessageFilter.JoinWarningTerm);
        if (!TestRoomMessageFilter.ShouldSuppress(true, message, translated)) return true;
        CalypsoExpansionSetPlugin.Instance!.Log.LogInfo("[Test room] suppressed only UI/lobby/waitPlayerBeforeStart; other messages remain enabled.");
        return false;
    }
}

[HarmonyPatch(typeof(ToolExtensionLinked), nameof(ToolExtensionLinked.OnStartServer))]
internal static class TestStandaloneLinkedIdolPatch
{
    private static bool Prefix(ToolExtensionLinked __instance)
    {
        // Native WaitDiceInitCoroutine only inserts this extension into its parent
        // SpawnTool cache. Ground-spawned reference idols have no such parent.
        if (!TestRoomLaunch.IsActive || __instance.spawnTool != null) return true;
        CalypsoExpansionSetPlugin.Instance!.Log.LogInfo("[Test room] standalone Linked idol has no SpawnTool parent; skipped that parent's cache-registration coroutine.");
        return false;
    }
}
