using CalypsoExpansionSet.Idols;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace CalypsoExpansionSet.Visuals;

// These layers can overlap while a released idol is easing out. Keep their
// lifetime in one place: apply from the normal POV outward, then unwind LIFO.
internal static class HeldCameraOverrides
{
    private static bool _restoring;

    internal static void BeginRender(Camera camera)
    {
        End();
        if (Testing.TestRoomAppearanceView.IsInspecting) return;
        try
        {
            WideBodyIdol.Begin(camera);
            UpsideDownIdol.Begin(camera);
            MonsterDisguisePreview.BeginRender(camera);
        }
        catch
        {
            End();
            throw;
        }
    }

    internal static void BeginInteraction(Camera camera)
    {
        End();
        // Native aim uses the held wide-body view, never the preview pose.
        WideBodyIdol.Begin(camera);
    }

    internal static void End(Camera? camera = null)
    {
        if (_restoring) return;
        _restoring = true;
        try
        {
            try { MonsterDisguisePreview.EndRender(camera); }
            finally
            {
                try { UpsideDownIdol.End(camera); }
                finally { WideBodyIdol.End(camera); }
            }
        }
        finally { _restoring = false; }
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "BeginCameraRendering")]
internal static class HeldCameraBeginPatch
{
    // Read the native/test-room camera parameters before adding visual layers.
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(Camera camera)
    {
        HeldCameraOverrides.BeginRender(camera);
        CalypsoExpansionSetPlugin.Instance?.Pings?.Capture(camera);
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "EndCameraRendering")]
internal static class HeldCameraEndPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(Camera camera)
    {
        CalypsoExpansionSetPlugin.Instance?.Pings?.FinishCamera(camera);
        HeldCameraOverrides.End(camera);
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "DoRenderLoop_Internal")]
internal static class HeldCameraFinallyPatch
{
    [HarmonyFinalizer, HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(Exception? __exception)
    {
        HeldCameraOverrides.End();
        return __exception;
    }
}
