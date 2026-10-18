using BoardGame.Elements;
using EnhancedEditor;
using Entities.Player;
using HarmonyLib;
using Items;
using Mirror;
using _200_Dev.BoardGameLogic;
using _200_Dev.BoardGameLogic.Curse;
using _200_Dev.Entities.Player.Behaviours.Inventory;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.Curses;

// Restrict the private marker to the native dice effect which consumes it.
[HarmonyPatch(typeof(SOFaceResultCellEffect), nameof(SOFaceResultCellEffect.SrvExecuteEffect))]
internal static class DisguiseDiceScopePatch
{
    [ThreadStatic] private static int _depth;
    internal static bool Active => _depth > 0;
    private static void Prefix(SOFaceResultCellEffect __instance, CheatTool cheatTool, out bool __state)
    {
        __state = NetworkServer.active;
        if (!__state) return;
        _depth++;
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"cell effect entered: dice={cheatTool.netId}, blessing={__instance.IsBlessing}");
    }
    private static Exception? Finalizer(Exception? __exception, bool __state, bool __result)
    {
        if (__state)
        {
            _depth--;
            if (DisguiseDiceDiagnostics.Enabled)
                DisguiseDiceDiagnostics.Write($"cell effect returned: accepted={__result}, error={__exception?.ToString() ?? "none"}");
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(CurseManager), nameof(CurseManager.TryGetRandomAlterationType))]
internal static class DisguiseDicePoolPatch
{
    private static bool Prefix(CurseManager __instance, ref bool __result, ref EAlterationType alterationType)
    {
        if (!DisguiseDiceScopePatch.Active) return true;
        var eligible = CalypsoExpansionSetPlugin.Instance?.Disguises?.CanRoll() == true;
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"random selection entered: disguiseEligible={eligible}");
        if (!eligible) return true;
        var pool = __instance.alterationTypesInfo;
        if (pool == null) return true;
        // CanRoll requires multiple spawned players, so the native solo-only tag
        // exclusion is inactive. Keep its per-entry filter and equal weighting.
        var noSoloTag = new Il2CppSystem.Nullable<Tag>();
        var candidates = new List<EAlterationType>(pool.Count + 1);
        foreach (var entry in pool)
        {
            if (entry.alterationType == DisguiseCurse.DiceMarker)
                throw new InvalidOperationException("Disguise dice marker conflicts with another curse.");
            if (entry.IsAllowedInDicePool(noSoloTag)) candidates.Add(entry.alterationType);
        }
        // FAlterationTypeInfo contains a reference. The generated List.Add wrapper
        // passes its boxed object header instead of unboxed data; never copy it.
        candidates.Add(DisguiseCurse.DiceMarker);
        alterationType = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        __result = true;
        if (DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"eligible candidates: native={candidates.Count - 1}, disguise=1");
        return false;
    }

    private static Exception? Finalizer(Exception? __exception, bool __result, EAlterationType alterationType)
    {
        if (DisguiseDiceScopePatch.Active && __exception == null)
            DisguiseDiceDiagnostics.LogSelection(__result, alterationType);
        if (DisguiseDiceScopePatch.Active && DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"random selection returned: found={__result}, type={(int)alterationType}, "
                + $"disguise={alterationType == DisguiseCurse.DiceMarker}, error={__exception?.ToString() ?? "none"}");
        return __exception;
    }
}

[HarmonyPatch(typeof(SOFaceResultCellEffect), nameof(SOFaceResultCellEffect.ApplyCursesToPlayers))]
internal static class DisguiseDiceApplyPatch
{
    private static bool Prefix(CheatTool cheatTool, PlayerEntity toolOwner, EAlterationType alterationType)
    {
        if (NetworkServer.active && DisguiseDiceDiagnostics.Enabled)
            DisguiseDiceDiagnostics.Write($"apply entered: dice={cheatTool.netId}, type={(int)alterationType}, "
                + $"disguise={alterationType == DisguiseCurse.DiceMarker}");
        if (alterationType != DisguiseCurse.DiceMarker) return true;
        // The native caller still handles the face's audio/result. Complete the
        // board turn using its no-pending-curse path; no unknown native enum escapes.
        if (NetworkServer.active)
        {
            var plugin = CalypsoExpansionSetPlugin.Instance;
            try
            {
                var reason = "Disguise service is unavailable.";
                if (plugin?.Disguises?.ApplyDice(out reason) != true)
                    plugin?.Log.LogWarning($"[Disguise dice] Selected but could not apply: {reason}");
            }
            catch (Exception error) { plugin?.Fail(error); }
            cheatTool.SrvTryEnqueueCheatTool(toolOwner, false);
            DisguiseDiceDiagnostics.Write("disguise die enqueue returned; requesting next turn");
            BoardGameManager.Instance?.BoardGameRulesEngine?.SrvNextTurn();
        }
        return false;
    }
}

// This is the same boundary at which vanilla removes non-perpetual alterations.
// It also runs for preserved-map events; model/scene recreation alone never clears a curse.
[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.SrvHandleZonePreChange))]
internal static class DisguiseRoundEndPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.ClearRound();
}

[HarmonyPatch(typeof(VhsNetworkManager), "SrvResetStateP1")]
internal static class DisguiseResetP1Patch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.ClearRound();
}
[HarmonyPatch(typeof(VhsNetworkManager), "SrvResetStateP2")]
internal static class DisguiseResetP2Patch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.ClearRound();
}
[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class DisguiseHandlersPatch
{
    private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.TransportReset();
}
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class DisguiseDisconnectPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.TransportReset(true);
}
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class DisguiseShutdownPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Disguises?.TransportReset(true);
}
