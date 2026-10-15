using CalypsoExpansionSet.Audio;
using CalypsoExpansionSet.Visuals;
using Entities.Monster;
using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Inventory;
using _200_Dev.Sound;

namespace CalypsoExpansionSet.Idols;

internal sealed partial class MonsterDisguiseRuntime
{
    private const byte AttackRequestKind = 2, AttackPlaybackKind = 3;
    private const int AttackMessageLength = 42;
    private const float AttackNoiseRange = 18f;
    private readonly NetworkMessageDelegate _requestHandler;
    private readonly Dictionary<uint, MonsterAttackProfile> _attackProfiles = new();
    private readonly Dictionary<int, AttackPeer> _attackPeers = new();
    private readonly Dictionary<uint, PlayingAttack> _attacks = new();
    private readonly List<int> _removedAttackPeers = new();
    private uint _requestSequence, _eventSequence, _receivedAttack;
    private float _nextAttackRequest;
    private sealed class AttackPeer
    {
        internal IntPtr Connection;
        internal uint Sequence;
        internal float NextAt;
    }
    private sealed record PlayingAttack(Choice Choice, ScoreTool Idol, PlayerEntity Holder, float Started, float Duration);

    private static uint Next(ref uint sequence) { sequence++; if (sequence == 0) sequence++; return sequence; }
    private static bool Newer(uint sequence, uint previous) => sequence != 0 && unchecked((int)(sequence - previous)) > 0;

    private static void WriteHeader(NetworkWriter writer, byte kind, CalypsoExpansionSetPlugin plugin)
    {
        writer.WriteUShort(MessageId); writer.WriteUInt(Magic); writer.WriteByte(Protocol); writer.WriteByte(kind);
        writer.WriteULong(plugin.Registry.Manifest); writer.WriteULong(MonsterDisguiseIdol.ConfigId);
    }
    private static void WriteChoice(NetworkWriter writer, Choice choice)
    { writer.WriteUInt(choice.Idol); writer.WriteUInt(choice.Holder); writer.WriteUInt(choice.Monster); writer.WriteUInt(choice.Revision); }
    private static Choice ReadChoice(NetworkReader reader) => new(reader.ReadUInt(), reader.ReadUInt(), reader.ReadUInt(), reader.ReadUInt());

    private MonsterAttackProfile AttackProfile(uint asset, MonsterEntity template)
    {
        if (_attackProfiles.TryGetValue(asset, out var profile) && profile.Template == template) return profile;
        if (_attackProfiles.Count >= MaximumInstances && !_attackProfiles.ContainsKey(asset))
            throw new InvalidOperationException("Monster attack profile capacity exceeded.");
        profile = new MonsterAttackProfile(template);
        _attackProfiles[asset] = profile;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin?.DebugTools.Enabled == true)
            plugin.Log.LogInfo($"[Monster attack resources] monster={template.name}, clip={profile.Clip?.name ?? "none"}, sound={profile.Sound.Guid}, duration={profile.Duration:F2}.");
        return profile;
    }

    internal static bool UseAttack(InventoryNew inventory)
    {
        var service = _instance;
        var player = EntityManager.Me;
        if (service == null || service._disposed || player == null || player.InventoryNew != inventory
            || !HeldAppearanceRuntime.Held(player, MonsterDisguiseIdol.ConfigId, out var idol)) return false;
        try
        {
            var context = new HeldIdolContext(idol!, player, true);
            if (!LocalHeldGuard.Ready(context, MonsterDisguiseIdol.ConfigId)
                || !service._client.TryGetValue(idol!.netId, out var choice) || choice.Holder != player.netId
                || Time.time < service._nextAttackRequest || service.AttackElapsed(choice).HasValue) return true;
            var template = NativeMonsterCatalog.Current.Resolve(choice.Monster);
            if (template == null) return true;
            var profile = service.AttackProfile(choice.Monster, template);
            if (!profile.Available) return true;
            var connection = NetworkClient.connection;
            if (connection == null || !connection.isAuthenticated) return true;
            service._nextAttackRequest = Time.time + profile.Interval;
            var writer = NetworkWriterPool.Get();
            try
            {
                WriteHeader(writer, AttackRequestKind, CalypsoExpansionSetPlugin.Instance!);
                writer.WriteUInt(Next(ref service._requestSequence)); WriteChoice(writer, choice);
                connection.Send(writer.ToArraySegment(), 0);
            }
            finally { writer.Dispose(); }
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
        return true;
    }

    private void ReceiveAttackRequest(NetworkConnection connection, NetworkReader reader, int channel)
    {
        if (_disposed) return;
        try
        {
            var plugin = CalypsoExpansionSetPlugin.Instance;
            var sender = connection.TryCast<NetworkConnectionToClient>();
            if (!NetworkServer.active || plugin == null || plugin.RuntimeFailed || !plugin.Registry.Ready
                || !Eligible(sender, plugin) || channel != 0 || Time.timeScale <= 0
                || !NetworkServer.connections.TryGetValue(sender!.connectionId, out var live) || live.Pointer != sender.Pointer
                || reader.Remaining != AttackMessageLength || reader.ReadUInt() != Magic
                || reader.ReadByte() != Protocol || reader.ReadByte() != AttackRequestKind
                || reader.ReadULong() != plugin.Registry.Manifest || reader.ReadULong() != MonsterDisguiseIdol.ConfigId) return;
            var sequence = reader.ReadUInt();
            var choice = ReadChoice(reader);
            if (!_server.TryGetValue(choice.Idol, out var row)
                || choice != new Choice(row.Idol.netId, row.Holder.netId, row.Monster, row.Revision)
                || !HeldAppearanceRuntime.Held(row.Holder, MonsterDisguiseIdol.ConfigId, out var held) || held != row.Idol
                || row.Holder.PlayerMovement?.connectionToClient?.Pointer != sender.Pointer
                || !NetworkServer.spawned.TryGetValue(choice.Idol, out var identity) || identity == null
                || identity.GetComponent<ScoreTool>() != row.Idol) return;
            if (!_attackPeers.TryGetValue(sender.connectionId, out var peer) || peer.Connection != sender.Pointer)
                _attackPeers[sender.connectionId] = peer = new AttackPeer { Connection = sender.Pointer };
            if (!Newer(sequence, peer.Sequence)) return;
            peer.Sequence = sequence;
            var now = Time.time;
            if (!float.IsFinite(now) || now < peer.NextAt) return;
            var template = NativeMonsterCatalog.Current.Resolve(choice.Monster);
            if (template == null) return;
            var profile = AttackProfile(choice.Monster, template);
            if (!profile.Available) return;
            peer.NextAt = now + profile.Interval;
            var writer = NetworkWriterPool.Get();
            try
            {
                WriteHeader(writer, AttackPlaybackKind, plugin);
                writer.WriteUInt(Next(ref _eventSequence)); WriteChoice(writer, choice);
                foreach (var target in NetworkServer.connections.Values)
                    if (Eligible(target, plugin)) target.Send(writer.ToArraySegment(), 0);
            }
            finally { writer.Dispose(); }
            // Native one-shots finish after release. Only this server emission
            // creates hearing noise; client playback never repeats it.
            var audio = NetworkAudioManager.Instance;
            var position = row.Holder.transform.position;
            if (!profile.Sound.IsNull && audio != null && audio.netId != 0
                && float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z))
            {
                foreach (var target in NetworkServer.connections.Values)
                    if (Eligible(target, plugin)) audio.TargetPlayOneShot(target, profile.Sound, position);
                IdolHearing.Emit(new HeldIdolContext(row.Idol, row.Holder, true),
                    MonsterDisguiseIdol.ConfigId, AttackNoiseRange, "Monster attack");
            }
            if (plugin.DebugTools.Enabled)
                plugin.Log.LogInfo($"[Monster attack] idol={choice.Idol}, holder={choice.Holder}, revision={choice.Revision}, monster={choice.Monster}.");
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
    }

    private void ReceiveAttackPlayback(NetworkReader reader)
    {
        if (reader.Remaining != 20) return;
        var sequence = reader.ReadUInt();
        var choice = ReadChoice(reader);
        if (!Newer(sequence, _receivedAttack)) return;
        _receivedAttack = sequence;
        if (!_client.TryGetValue(choice.Idol, out var current) || current != choice
            || !NetworkClient.spawned.TryGetValue(choice.Holder, out var identity) || identity == null) return;
        var holder = identity.GetComponent<PlayerEntity>();
        if (!HeldAppearanceRuntime.Held(holder, MonsterDisguiseIdol.ConfigId, out var idol) || idol!.netId != choice.Idol) return;
        var template = NativeMonsterCatalog.Current.Resolve(choice.Monster);
        if (template == null) return;
        var profile = AttackProfile(choice.Monster, template);
        if (!profile.Available || AttackElapsed(choice).HasValue) return;
        _attacks[choice.Idol] = new PlayingAttack(choice, idol, holder!, Time.time, profile.Duration);
        if (holder == EntityManager.Me) _nextAttackRequest = Time.time + profile.Interval;
    }

    // Keep closure allocation in this factory, after the world-view guard.
    private Func<float?> AttackClock(Choice choice) => () => AttackElapsed(choice);

    private float? AttackElapsed(Choice choice)
    {
        if (!_attacks.TryGetValue(choice.Idol, out var playing) || playing.Choice != choice
            || !_client.TryGetValue(choice.Idol, out var current) || current != choice
            || !HeldAppearanceRuntime.Held(playing.Holder, MonsterDisguiseIdol.ConfigId, out var idol) || idol != playing.Idol) return null;
        var elapsed = Time.time - playing.Started;
        return elapsed >= 0 && elapsed < playing.Duration ? elapsed : null;
    }

    private void TickAttacks()
    {
        _removed.Clear();
        foreach (var pair in _attacks) if (!AttackElapsed(pair.Value.Choice).HasValue) _removed.Add(pair.Key);
        foreach (var id in _removed) _attacks.Remove(id);
        _removedAttackPeers.Clear();
        foreach (var pair in _attackPeers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var connection) || connection.Pointer != pair.Value.Connection)
                _removedAttackPeers.Add(pair.Key);
        foreach (var id in _removedAttackPeers) _attackPeers.Remove(id);
    }
}

[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.CliOnUseInput))]
internal static class MonsterDisguiseAttackInputPatch
{
    // The native UseItem action already covers mouse, gamepad and rebinding.
    [HarmonyPrefix] private static bool Prefix(InventoryNew __instance) => !MonsterDisguiseRuntime.UseAttack(__instance);
}
