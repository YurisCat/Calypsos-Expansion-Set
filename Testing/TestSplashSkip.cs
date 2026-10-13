using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace CalypsoExpansionSet.Testing;

internal static class TestSplashSkip
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CancelSplash();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool SplashFinished();

    internal static void TryStop(CalypsoExpansionSetPlugin plugin)
    {
        try
        {
            // Unity 6000.3 SplashScreen.Stop(StopImmediate) calls this zero-argument
            // engine binding. Its managed class is stripped from this game's interop.
            var finished = IL2CPP.ResolveICall<SplashFinished>("UnityEngine.Rendering.SplashScreen::get_isFinished");
            var cancel = IL2CPP.ResolveICall<CancelSplash>("UnityEngine.Rendering.SplashScreen::CancelSplashScreen");
            var wasFinished = finished();
            cancel();
            plugin.Log.LogInfo($"[Test room] Unity logo stop: wasFinished={wasFinished}, isFinished={finished()}.");
        }
        catch (Exception exception)
        {
            plugin.Log.LogWarning($"[Test room] Unity logo stop unavailable; normal loading continues: {exception.Message}");
        }
    }
}
