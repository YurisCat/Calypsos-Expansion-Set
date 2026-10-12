using Entities.Player;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;

namespace MoreIdols;

internal sealed class IdolEffectRuntime : IDisposable
{
    private sealed record Running(ScoreTool Idol, PlayerEntity Holder, HeldIdolEffect Effect);
    private readonly MoreIdolsPlugin _plugin;
    private readonly Dictionary<uint, Running> _active = new();
    private readonly HashSet<uint> _seen = new();
    private readonly Dictionary<uint, IntPtr> _faulted = new();
    private readonly List<uint> _removed = new();
    private readonly List<JumpingIdolSync.TrackedIdol> _serverSnapshot = new();
    internal IdolEffectRuntime(MoreIdolsPlugin plugin) => _plugin = plugin;

    internal void Tick()
    {
        _seen.Clear();
        if (_plugin.Registry.Ready)
        {
            var player = NetworkClient.isConnected && NetworkClient.ready && _plugin.Sync.ClientCompatible ? EntityManager.Me : null;
            var item = player != null && player.InventoryNew != null ? player.InventoryNew.InHand : null;
            var idol = item != null ? item.TryCast<ScoreTool>() : null;
            if (player != null && player.isActiveAndEnabled && player.CurrentHealthSync > 0
                && idol != null && idol.ItemOwner == player && idol.SoItem != null
                && _plugin.Registry.TryGet(idol.SoItem.Id, out var entry)
                && entry.Definition.Authority != IdolEffectAuthority.ServerGameplay)
            {
                var movement = player!.PlayerMovement;
                if (movement != null && movement.isOwned)
                    Run(entry, idol, player, _plugin.LocalGameplayReady(player));
            }
            if (NetworkServer.active)
            {
                _serverSnapshot.Clear();
                _serverSnapshot.AddRange(_plugin.Sync.ServerIdols);
                // A server effect can spawn/destroy an idol and change the tracked collection.
                foreach (var tracked in _serverSnapshot)
                {
                    idol = tracked.Idol;
                    if (idol == null || idol.SoItem == null || !_plugin.Registry.TryGet(idol.SoItem.Id, out entry)
                        || entry.Definition.Authority != IdolEffectAuthority.ServerGameplay) continue;
                    player = idol.ItemOwner != null ? idol.ItemOwner.TryCast<PlayerEntity>() : null;
                    if (player != null && player.isActiveAndEnabled && player.CurrentHealthSync > 0
                        && player.InventoryNew != null && player.InventoryNew.InHand == idol)
                        Run(entry, idol, player, Time.timeScale > 0f && player.isActiveAndEnabled);
                }
            }
        }
        _removed.Clear();
        foreach (var id in _active.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) Stop(id);
        _removed.Clear();
        foreach (var id in _faulted.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _faulted.Remove(id);
    }

    private void Run(RegisteredIdol entry, ScoreTool idol, PlayerEntity holder, bool gameplayReady)
    {
        var id = idol.netId;
        if (id == 0) return;
        _seen.Add(id);
        if (_active.TryGetValue(id, out var running) && (running.Idol != idol || running.Holder != holder))
        {
            Stop(id);
            _faulted.Remove(id);
            running = null;
        }
        if (_faulted.TryGetValue(id, out var faultHolder))
        {
            if (faultHolder == holder.Pointer) return;
            _faulted.Remove(id);
        }
        var context = new HeldIdolContext(idol, holder, gameplayReady);
        try
        {
            if (running == null)
            {
                var effect = entry.Definition.CreateEffect() ?? throw new InvalidOperationException("Effect factory returned null.");
                running = new Running(idol, holder, effect);
                _active.Add(id, running);
                effect.Start(context);
                _plugin.Log.LogInfo($"[Effect start] {entry.Definition.Key}: netId={id}, holder={holder.netId}, authority={entry.Definition.Authority}.");
            }
            running.Effect.Tick(context);
        }
        catch (Exception exception)
        {
            Stop(id);
            _faulted[id] = holder.Pointer;
            _plugin.Log.LogError($"Effect {entry.Definition.Key} disabled for netId={id} until released: {exception}");
        }
    }

    private void Stop(uint id)
    {
        if (!_active.Remove(id, out var running)) return;
        try { running.Effect.Dispose(); }
        catch (Exception exception) { _plugin.Log.LogError($"Effect cleanup failed for netId={id}: {exception}"); }
        _plugin.Log.LogInfo($"[Effect stop] netId={id}.");
    }

    public void Dispose()
    {
        foreach (var id in _active.Keys.ToArray()) Stop(id);
        _faulted.Clear();
    }
}
