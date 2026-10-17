using System.Reflection;
using EnhancedEditor;
using EnhancedFramework.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using _200_Dev.Entities;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;
using _200_Dev.UI.Networking;
using _200_Dev.BoardGameLogic.Elements.Tool.ToolExtension;

namespace CalypsoExpansionSet.Testing;

// Gallery-only patches use the dedicated launch; save/connection guards also cover explicit local tests.
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
    private static bool Prefix() => !TestRoomLaunch.IsActive && !LocalMultiplayer.IsActive;
}

[HarmonyPatch(typeof(VhsNetworkManager), nameof(VhsNetworkManager.OnServerConnect))]
internal static class TestLocalConnectionOnlyPatch
{
    private static bool Prefix(NetworkConnectionToClient conn)
    {
        if (conn.TryCast<LocalConnectionToClient>() != null) return true;
        if (!TestRoomLaunch.IsActive && (!LocalMultiplayer.IsActive || LocalMultiplayer.IsLoopback(conn.address))) return true;
        CalypsoExpansionSetPlugin.Instance!.Log.LogWarning(TestRoomLaunch.IsActive
            ? "[Test room] remote connection refused: this procedural gallery is single-player."
            : "[Local multiplayer] connection refused: only loopback clients may join this test session.");
        conn.Disconnect();
        return false;
    }
}

[HarmonyPatch(typeof(VhsNetworkManager), nameof(VhsNetworkManager.ResolveNickName))]
internal static class LocalMultiplayerNicknamePatch
{
    private static bool Prefix(ref string __result)
    {
        var nickname = LocalMultiplayer.SessionNickname;
        if (nickname == null) return true;
        __result = nickname;
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
        if ((!TestRoomLaunch.IsActive && !IdolDebugCatalog.SpawningOriginal) || __instance.spawnTool != null) return true;
        CalypsoExpansionSetPlugin.Instance!.Log.LogInfo("[Debug spawn] standalone Linked idol has no SpawnTool parent; skipped that parent's cache-registration coroutine.");
        return false;
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "BeginCameraRendering")]
internal static class TestRoomViewDistanceBeginPatch
{
    [HarmonyPostfix]
    private static void Postfix(Camera camera)
        => CalypsoExpansionSetPlugin.Instance?.TestRoom?.ViewDistance?.Begin(camera);
}

[HarmonyPatch(typeof(RenderPipelineManager), "EndCameraRendering")]
internal static class TestRoomViewDistanceEndPatch
{
    [HarmonyPrefix]
    private static void Prefix(Camera camera)
        => CalypsoExpansionSetPlugin.Instance?.TestRoom?.ViewDistance?.End(camera);
}

[HarmonyPatch(typeof(RenderPipelineManager), "DoRenderLoop_Internal")]
internal static class TestRoomViewDistanceFinallyPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        CalypsoExpansionSetPlugin.Instance?.TestRoom?.ViewDistance?.End();
        return __exception;
    }
}
