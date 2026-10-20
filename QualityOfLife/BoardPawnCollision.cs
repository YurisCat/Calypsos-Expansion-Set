using HarmonyLib;
using Mirror;
using UnityEngine;
using _200_Dev.BoardGameLogic;
using _200_Dev.BoardGameLogic.Elements.Pawns;

namespace CalypsoExpansionSet.QualityOfLife;

// Only the progress pawns owned by the active board are eligible. Never change
// a prefab, the board surface, dice, or EnvironmentElements.ChessboardPiece.
internal sealed class BoardPawnCollision : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly RuntimeFailureGate _recovery = new();
    private readonly Dictionary<int, Collider> _owned = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _removed = new();
    private float _nextMaintenance;
    private bool _disposed, _failed;

    internal BoardPawnCollision(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    private bool CanApply => !_disposed && !_failed
        && (NetworkServer.active || NetworkClient.active);

    internal void Tick()
    {
        if (_disposed) return;
        try
        {
            if (_failed)
            {
                if (!_recovery.TryResume(BoardGameManager.Instance)) return;
                _failed = false;
            }
            if (!CanApply) { RestoreAll(); _nextMaintenance = 0; return; }
            if (Time.realtimeSinceStartup < _nextMaintenance) return;
            _nextMaintenance = Time.realtimeSinceStartup + 1f;
            Refresh(BoardGameManager.Instance?.PawnManager);
        }
        catch (Exception exception) { Fail(exception); }
    }

    internal void ApplyBoard(PawnManager? manager)
    {
        if (!CanApply) return;
        try
        {
            if (manager != null && manager == BoardGameManager.Instance?.PawnManager)
                Refresh(manager);
        }
        catch (Exception exception) { Fail(exception); }
    }

    internal void ApplyDisplayedPawn(Pawn pawn)
    {
        if (!CanApply) return;
        try
        {
            // The display RPC also runs after reactivation. Check membership to
            // avoid acting on unrelated Pawn components supplied by another MOD.
            var pawns = BoardGameManager.Instance?.PawnManager?.pawns;
            if (pawns == null) return;
            for (var index = 0; index < pawns.Length; index++)
                if (pawns[index] == pawn) { Disable(pawn); return; }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private void Refresh(PawnManager? manager)
    {
        _seen.Clear();
        var pawns = manager != null ? manager.pawns : null;
        if (pawns != null)
            for (var index = 0; index < pawns.Length; index++)
            {
                var pawn = pawns[index];
                if (pawn == null) continue;
                var collider = pawn.Collider;
                if (collider == null || collider.isTrigger) continue;
                _seen.Add(collider.GetInstanceID());
                Disable(pawn);
            }
        _removed.Clear();
        foreach (var pair in _owned)
            if (pair.Value == null || !_seen.Contains(pair.Key)) _removed.Add(pair.Key);
        foreach (var id in _removed) { Restore(_owned[id]); _owned.Remove(id); }
    }

    private void Disable(Pawn pawn)
    {
        if (pawn == null) return;
        var collider = pawn.Collider;
        if (collider == null || collider.isTrigger || !collider.enabled) return;
        // Remember only transitions we own. A collider already disabled by the
        // game or another MOD must never be enabled by our cleanup.
        _owned[collider.GetInstanceID()] = collider;
        collider.enabled = false;
        if (_plugin.DebugTools.Enabled)
            _plugin.Log.LogInfo($"[Board pawn collision] disabled progress pawn collider: {pawn.name}.");
    }

    private void Restore(Collider? collider)
    {
        try { if (collider != null && !collider.enabled) collider.enabled = true; }
        catch (Exception exception)
        { _plugin.Log.LogWarning($"[Board pawn collision] could not restore a released collider: {exception.Message}"); }
    }

    private void RestoreAll()
    {
        foreach (var collider in _owned.Values) Restore(collider);
        _owned.Clear();
        _seen.Clear();
        _removed.Clear();
    }

    private void Fail(Exception exception)
    {
        if (_failed) return;
        _failed = true;
        _recovery.Block(BoardGameManager.Instance);
        RestoreAll();
        _plugin.Log.LogError($"Board pawn collision service suspended after an error; will retry after scene, connection or board replacement.\n{exception}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreAll();
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.CliDisplayPawn))]
internal static class ProgressPawnDisplayedPatch
{
    private static void Postfix(Pawn __instance) =>
        CalypsoExpansionSetPlugin.Instance?.PawnCollisions?.ApplyDisplayedPawn(__instance);
}

[HarmonyPatch(typeof(BoardGameManager), nameof(BoardGameManager.OnStartClient))]
internal static class ProgressBoardClientStartedPatch
{
    private static void Postfix(BoardGameManager __instance) =>
        CalypsoExpansionSetPlugin.Instance?.PawnCollisions?.ApplyBoard(__instance.PawnManager);
}

[HarmonyPatch(typeof(PawnManager), nameof(PawnManager.OnStartServer))]
internal static class ProgressPawnServerStartedPatch
{
    private static void Postfix(PawnManager __instance) =>
        CalypsoExpansionSetPlugin.Instance?.PawnCollisions?.ApplyBoard(__instance);
}
