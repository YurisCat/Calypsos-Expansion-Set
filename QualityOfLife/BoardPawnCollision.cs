using BepInEx.Configuration;
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
    private readonly ConfigEntry<bool> _enabled;
    private readonly Dictionary<int, Collider> _owned = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _removed = new();
    private float _nextMaintenance;
    private bool _disposed, _failed;

    internal BoardPawnCollision(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _enabled = BindSetting(plugin.Config);
    }

    private static ConfigEntry<bool> BindSetting(ConfigFile config)
    {
        if (config.TryGetEntry<bool>("QualityOfLife", "DisableBoardPawnCollision", out var existing)) return existing;
        var saveOnSet = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        try
        {
            // Read raw values to distinguish missing keys, prefer the new key,
            // and keep the Boolean setting's documented default fixed at true.
            var legacy = config.Bind("QualityOfLife", "关闭棋盘上棋子的碰撞箱", "");
            var stored = config.Bind("QualityOfLife", "DisableBoardPawnCollision", legacy.Value);
            var value = stored.Value;
            config.Remove(legacy.Definition);
            config.Remove(stored.Definition);
            var setting = config.Bind("QualityOfLife", "DisableBoardPawnCollision", true,
                "关闭棋盘进度棋子的实体碰撞，方便移动和踢骰子；显示、动画、棋盘、骰子及事件棋子不变。各端独立生效，联机建议一致；修改后重启。\n"
                + "/ Disable board progress-pawn collisions for easier movement and dice kicking; visuals, animations, board, dice and event pieces stay unchanged. Local setting; matching settings recommended. Restart after changes.");
            if (!string.IsNullOrWhiteSpace(value)) setting.SetSerializedValue(value);
            return setting;
        }
        finally { config.SaveOnConfigSet = saveOnSet; }
    }

    private bool CanApply => !_disposed && !_failed && _enabled.Value
        && (NetworkServer.active || NetworkClient.active);

    internal void Tick()
    {
        if (_disposed || _failed) return;
        if (!CanApply) { RestoreAll(); _nextMaintenance = 0; return; }
        if (Time.realtimeSinceStartup < _nextMaintenance) return;
        _nextMaintenance = Time.realtimeSinceStartup + 1f;
        try { Refresh(BoardGameManager.Instance?.PawnManager); }
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
        _failed = true;
        RestoreAll();
        _plugin.Log.LogError($"Board pawn collision option disabled after an error; other MOD features continue. Restart after fixing it.\n{exception}");
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
