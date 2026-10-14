using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using User;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Testing;

// Explicit single-player test-room diagnostic. No normal-game camera change.
internal static class TestRoomAppearanceView
{
    private static bool _requested, _oldWorldRig;
    private static PlayerEntity? _player;
    private static Camera? _camera;
    private static Vector3 _beforePosition, _writtenPosition;
    private static Quaternion _beforeRotation, _writtenRotation;
    internal static bool IsInspecting => _player != null;

    internal static bool Toggle()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (!TestRoomLaunch.IsActive || plugin?.DebugTools.Enabled != true || !NetworkServer.active)
            return false;
        _requested = !_requested;
        if (!_requested) Reset();
        plugin.DebugTools.Report($"[Test appearance] inspection view {(_requested ? "on" : "off")}; use inspect again to restore.");
        return true;
    }

    internal static void Prepare()
    {
        try { PrepareCore(); }
        catch (Exception error) { Fault(error); }
    }

    private static void PrepareCore()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var player = EntityManager.Me;
        if (!_requested || !TestRoomLaunch.IsActive || plugin?.DebugTools.Enabled != true
            || plugin.RuntimeFailed || !NetworkClient.ready || player == null || player.CurrentHealthSync <= 0)
        { _requested = false; Reset(); return; }
        if (!plugin.LocalGameplayReady(player)) { Reset(); return; }
        if (_player != player)
        {
            Reset();
            _player = player;
            _oldWorldRig = player.UseFreeCamWorldRig;
            player.CliSetFreeCamWorldRig(true);
            var rig = player.PlayerWorldRigController;
            plugin.Log.LogInfo($"[Test appearance rig] world={rig?.RigGameObject?.name}, active={rig?.RigGameObject?.activeInHierarchy}, renderer={rig?.SkinnedMeshRenderer?.name}, enabled={rig?.SkinnedMeshRenderer?.enabled}, off={rig?.SkinnedMeshRenderer?.forceRenderingOff}; local={player.PlayerLocalRigController?.RigGameObject?.activeInHierarchy}.");
        }
    }

    internal static void Begin(Camera camera)
    {
        try { BeginCore(camera); }
        catch (Exception error) { Fault(error); }
    }

    private static void BeginCore(Camera camera)
    {
        if (_player == null || SetFovShakeData.Instance?._cam != camera) return;
        End();
        var target = _player.transform.position + Vector3.up;
        var forward = Vector3.ProjectOnPlane(_player.transform.forward, Vector3.up).normalized;
        _beforePosition = camera.transform.position;
        _beforeRotation = camera.transform.rotation;
        _camera = camera;
        _writtenPosition = target + forward * 3f + Vector3.up * 0.2f;
        _writtenRotation = Quaternion.LookRotation(target - _writtenPosition);
        camera.transform.SetPositionAndRotation(_writtenPosition, _writtenRotation);
    }

    internal static void End()
    {
        var camera = _camera; _camera = null;
        if (camera == null) return;
        if (camera.transform.position == _writtenPosition) camera.transform.position = _beforePosition;
        if (camera.transform.rotation == _writtenRotation) camera.transform.rotation = _beforeRotation;
    }

    private static void Reset()
    {
        End();
        if (_player != null && _player.UseFreeCamWorldRig)
            _player.CliSetFreeCamWorldRig(_oldWorldRig);
        _player = null;
    }

    internal static void Stop()
    {
        _requested = false;
        try { Reset(); }
        catch (Exception error)
        {
            _player = null; _camera = null;
            CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Test appearance cleanup failed: {error}");
        }
    }

    private static void Fault(Exception error)
    {
        Stop();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Test appearance view stopped: {error}");
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "BeginContextRendering")]
internal static class TestAppearancePreparePatch
{
    [HarmonyPrefix] private static void Prefix() => TestRoomAppearanceView.Prepare();
}
[HarmonyPatch(typeof(RenderPipelineManager), "BeginCameraRendering")]
internal static class TestAppearanceBeginPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(Camera camera) => TestRoomAppearanceView.Begin(camera);
}
[HarmonyPatch(typeof(RenderPipelineManager), "EndCameraRendering")]
internal static class TestAppearanceEndPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix() => TestRoomAppearanceView.End();
}
[HarmonyPatch(typeof(RenderPipelineManager), "DoRenderLoop_Internal")]
internal static class TestAppearanceFinallyPatch
{
    [HarmonyFinalizer, HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(Exception? __exception) { TestRoomAppearanceView.End(); return __exception; }
}
