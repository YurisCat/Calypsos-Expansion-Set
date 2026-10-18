using BoardGame.Systems;
using HarmonyLib;
using Mirror;
using _200_Dev.BoardGameLogic;
using _200_Dev.BoardGameLogic.Curse;

namespace CalypsoExpansionSet.Curses;

// Read-only lifecycle traces: a stalled dice flow need not throw an exception.
internal static class DisguiseDiceDiagnostics
{
    internal static bool Enabled => NetworkServer.active
        && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true;

    // One host-side line per dice curse roll, including when DebugMode is off.
    internal static void LogSelection(bool found, EAlterationType type)
    {
        if (!NetworkServer.active) return;
        var name = !found ? "none (no eligible curse)"
            : type == DisguiseCurse.DiceMarker ? $"Disguise ({DisguiseCurse.Key}, {(int)type})"
            : $"{type} ({(int)type})";
        CalypsoExpansionSetPlugin.Instance?.Log.LogInfo($"[Dice curse] selected={name}.");
    }

    internal static void Write(string stage, CurseManager? curses = null)
    {
        if (!Enabled) return;
        var plugin = CalypsoExpansionSetPlugin.Instance!;
        try
        {
            var board = BoardGameManager.Instance;
            var waiting = new List<int>();
            if (curses != null && curses._cursesApplied != null)
                foreach (var entry in curses._cursesApplied)
                    if (!entry.Value) waiting.Add(entry.Key);
            plugin.Log.LogInfo($"[Disguise dice] {stage}; board={(board != null ? board.BoardGameState.ToString() : "missing")}, "
                + $"waitingConnections=[{string.Join(",", waiting)}].");
        }
        catch (Exception error)
        {
            // A diagnostic snapshot must never interrupt a native turn.
            plugin.Log.LogWarning($"[Disguise dice] Diagnostic snapshot unavailable: {error.Message}");
        }
    }
}

[HarmonyPatch(typeof(DiceTool), nameof(DiceTool.SrvExecuteDicePostThrowEffect))]
internal static class DisguiseDiceExecutionTracePatch
{
    private static void Prefix(DiceTool __instance, out bool __state)
    {
        __state = DisguiseDiceDiagnostics.Enabled;
        if (__state) DisguiseDiceDiagnostics.Write($"dice execution entered: dice={__instance.netId}, hitBoard={__instance.HitBoard}");
    }

    private static Exception? Finalizer(bool __state, bool __result, Exception? __exception)
    {
        if (__state) DisguiseDiceDiagnostics.Write($"dice execution returned: accepted={__result}, error={__exception?.ToString() ?? "none"}");
        return __exception;
    }
}

[HarmonyPatch(typeof(DiceTool), nameof(DiceTool.CliGetFaceResultByDiceRotationIndex))]
internal static class DisguiseDiceFaceTracePatch
{
    private static void Postfix(DiceTool __instance, SOFaceResult __result)
    {
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"face resolved: dice={__instance.netId}, effect={(__result != null ? __result.name : "missing")}");
    }
}

[HarmonyPatch(typeof(CurseManager), nameof(CurseManager.SrvSpawnCurses))]
internal static class DisguiseNativeCurseSpawnTracePatch
{
    private static void Prefix(CurseManager __instance)
        => DisguiseDiceDiagnostics.Write("native curse spawning entered", __instance);

    private static Exception? Finalizer(CurseManager __instance, Exception? __exception)
    {
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"native curse spawning returned: error={__exception?.ToString() ?? "none"}", __instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(CurseManager), nameof(CurseManager.SrvOnLocalCurseApplied))]
internal static class DisguiseNativeCurseArrivalTracePatch
{
    private static void Postfix(CurseManager __instance)
        => DisguiseDiceDiagnostics.Write("native curse arrival handled", __instance);
}

[HarmonyPatch(typeof(CurseManager), "SrvOnAllCursesApplied")]
internal static class DisguiseNativeCurseCompleteTracePatch
{
    private static void Prefix(CurseManager __instance)
        => DisguiseDiceDiagnostics.Write("native curses complete; requesting next turn", __instance);
}

[HarmonyPatch(typeof(BoardGameRulesEngine), nameof(BoardGameRulesEngine.SrvNextTurn))]
internal static class DisguiseDiceNextTurnTracePatch
{
    private static void Prefix() => DisguiseDiceDiagnostics.Write("next turn entered");

    private static Exception? Finalizer(Exception? __exception)
    {
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"next turn returned: error={__exception?.ToString() ?? "none"}");
        return __exception;
    }
}
