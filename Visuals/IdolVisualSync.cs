using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Visuals;

// Shared presentation only. Types provide their own visual factory; this layer
// never interprets a value as movement, fatigue, health or an eye color.
internal sealed class IdolVisualSync : IDisposable
{
    internal const ushort MessageId = 0x4D4D;
    internal const uint Magic = 0x43455356;
    internal const byte Protocol = 1, ReportKind = 1, SnapshotKind = 2;
    internal const int ReportLength = 38, SnapshotHeaderLength = 20, EntryLength = 20, MaximumInstances = 512;
    internal const float SnapshotInterval = 0.2f, ReportInterval = 0.1f, HeartbeatInterval = 0.3f, HolderTimeout = 1f;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly NetworkMessageDelegate _serverHandler, _clientHandler;
    private readonly record struct Published(ScoreTool Idol, uint Holder, IntPtr Connection, float Value, float Updated);
    internal readonly record struct Entry(uint Idol, ulong Config, uint Holder, float Value);
    private readonly record struct Peer(IntPtr Connection, uint Sequence, float AcceptedAt);
    private sealed record ClientRow(ScoreTool Idol, Entry State);
    private sealed record Drawing(ScoreTool Idol, IIdolStateVisual Visual)
    { internal float Value = float.NaN, NextLog; }
    private sealed class Proposal
    {
        internal readonly ScoreTool Idol;
        internal float Value, Updated, SentAt = float.NegativeInfinity, SentValue = float.NaN;
        internal Proposal(ScoreTool idol) => Idol = idol;
    }
    private readonly Dictionary<uint, Published> _published = new();
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly Dictionary<uint, ClientRow> _client = new();
    private readonly Dictionary<uint, Drawing> _drawings = new();
    private readonly Dictionary<uint, IntPtr> _faulted = new();
    private readonly HashSet<uint> _seen = new(), _incomingIds = new();
    private readonly List<uint> _removed = new();
    private readonly List<int> _removedPeers = new();
    private readonly List<Entry> _outgoing = new();
    private readonly Entry[] _incoming = new Entry[MaximumInstances];
    private Proposal? _proposal;
    private IntPtr _connection;
    private uint _requestSequence, _snapshotSequence, _receivedSequence;
    private float _nextSnapshot;
    private bool _serverActive, _disposed;

    internal IdolVisualSync(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _serverHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveReport;
        _clientHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveSnapshot;
        try { EnsureHandlers(); }
        catch { Dispose(); throw; }
    }

    internal void Publish(ScoreTool idol, float value)
    {
        if (!ValidValue(value)) throw new ArgumentOutOfRangeException(nameof(value), "Visual state must be finite and within [0,1].");
        if (_disposed || _plugin.RuntimeFailed || !_plugin.Registry.Ready || !Definition(idol, out var definition)) return;
        if (definition.Authority == IdolVisualAuthority.Server)
        {
            if (!NetworkServer.active || !ServerItem(idol)) return;
            _published[idol.netId] = new Published(idol, 0, IntPtr.Zero, value, Time.realtimeSinceStartup);
        }
        else if (LocalHolder(idol, out _))
        {
            if (_proposal?.Idol != idol) _proposal = new Proposal(idol);
            _proposal.Value = value;
            _proposal.Updated = Time.realtimeSinceStartup;
        }
    }

    private bool Definition(ScoreTool? idol, out IdolSharedVisualDefinition definition)
    {
        definition = null!;
        if (idol == null || idol.netId == 0 || idol.SoItem == null
            || !_plugin.Registry.TryGet(idol.SoItem.Id, out var registered) || registered.Definition.SharedVisual == null) return false;
        definition = registered.Definition.SharedVisual;
        return true;
    }

    private static bool ServerItem(ScoreTool idol) => NetworkServer.spawned.TryGetValue(idol.netId, out var identity)
        && identity != null && identity.GetComponent<ScoreTool>() == idol;

    private static PlayerEntity? Holder(ScoreTool idol)
    {
        var holder = idol.ItemOwner?.TryCast<PlayerEntity>();
        return holder != null && holder.isActiveAndEnabled && holder.CurrentHealthSync > 0
            && holder.PlayerMovement != null && holder.InventoryNew != null && holder.InventoryNew.InHand == idol ? holder : null;
    }

    private bool LocalHolder(ScoreTool idol, out PlayerEntity holder)
    {
        holder = Holder(idol)!;
        return NetworkClient.isConnected && NetworkClient.ready && _plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.PlayerMovement.isOwned;
    }

    private bool Eligible(NetworkConnectionToClient? peer) => peer != null && peer.isAuthenticated && peer.isReady
        && peer.identity != null && _plugin.Sync.IsCompatible(peer)
        && NetworkServer.connections.TryGetValue(peer.connectionId, out var current) && current.Pointer == peer.Pointer;

    internal static bool ValidValue(float value) => float.IsFinite(value) && value is >= 0 and <= 1;
    internal static bool Newer(uint value, uint previous) => value != 0 && unchecked((int)(value - previous)) > 0;
    private static uint Next(ref uint value) { value++; if (value == 0) value++; return value; }

    internal void Tick()
    {
        if (_disposed) return;
        if (_plugin.RuntimeFailed) { Dispose(); return; }
        EnsureHandlers();
        var connection = NetworkClient.connection;
        if (!NetworkClient.isConnected || !NetworkClient.ready || connection == null
            || !connection.isAuthenticated || !_plugin.Sync.ClientCompatible)
            ClearClient();
        else
        {
            if (_connection != connection.Pointer) { ClearClient(); _connection = connection.Pointer; }
            SendProposal();
        }
        if (NetworkServer.active && _plugin.Registry.Ready) TickServer();
        else if (_serverActive) ClearServer();
        if (_connection != IntPtr.Zero) TickVisuals();
    }

    private void SendProposal()
    {
        var proposal = _proposal;
        if (proposal == null) return;
        var now = Time.realtimeSinceStartup;
        if (proposal.Idol == null || now - proposal.Updated > HolderTimeout * 0.5f
            || !Definition(proposal.Idol, out var definition) || definition.Authority != IdolVisualAuthority.Holder
            || !LocalHolder(proposal.Idol, out var holder))
        { _proposal = null; return; }
        if (now - proposal.SentAt < ReportInterval
            || proposal.Value == proposal.SentValue && now - proposal.SentAt < HeartbeatInterval) return;
        var writer = NetworkWriterPool.Get();
        try
        {
            Header(writer, ReportKind);
            writer.WriteULong(proposal.Idol.SoItem.Id);
            writer.WriteUInt(Next(ref _requestSequence));
            writer.WriteUInt(proposal.Idol.netId);
            writer.WriteUInt(holder.netId);
            writer.WriteFloat(proposal.Value);
            NetworkClient.connection!.Send(writer.ToArraySegment(), 0);
            proposal.SentAt = now; proposal.SentValue = proposal.Value;
        }
        finally { writer.Dispose(); }
    }

    private void ReceiveReport(NetworkConnection connection, NetworkReader reader, int channel)
    {
        if (_disposed || !NetworkServer.active || !_plugin.Registry.Ready || _plugin.RuntimeFailed || channel != 0) return;
        var peer = connection.TryCast<NetworkConnectionToClient>();
        if (!Eligible(peer) || reader.Remaining != ReportLength || !ReadHeader(reader, ReportKind)) return;
        var config = reader.ReadULong();
        var sequence = reader.ReadUInt();
        var id = reader.ReadUInt();
        var claimedHolder = reader.ReadUInt();
        var value = reader.ReadFloat();
        if (!ValidValue(value) || sequence == 0 || id == 0
            || !NetworkServer.spawned.TryGetValue(id, out var identity) || identity == null) return;
        var idol = identity.GetComponent<ScoreTool>();
        if (!Definition(idol, out var definition) || idol!.SoItem.Id != config
            || definition.Authority != IdolVisualAuthority.Holder) return;
        var holder = Holder(idol);
        if (holder == null || holder.netId != claimedHolder || holder.PlayerMovement.connectionToClient?.Pointer != peer!.Pointer) return;
        var now = Time.realtimeSinceStartup;
        if (_peers.TryGetValue(peer!.connectionId, out var prior) && prior.Connection == peer.Pointer
            && (!Newer(sequence, prior.Sequence) || now - prior.AcceptedAt < ReportInterval * 0.5f)) return;
        _peers[peer.connectionId] = new Peer(peer.Pointer, sequence, now);
        if (_plugin.DebugTools.Enabled && (!_published.TryGetValue(id, out var previous)
            || previous.Holder != holder.netId || previous.Value != value))
            _plugin.Log.LogInfo($"[Visual report] idol={id}, holder={holder.netId}, value={value:F3}; owner validated.");
        _published[id] = new Published(idol, holder.netId, peer.Pointer, value, now);
    }

    private void TickServer()
    {
        _serverActive = true;
        var now = Time.realtimeSinceStartup;
        if (now < _nextSnapshot) return;
        _nextSnapshot = now + SnapshotInterval;
        _outgoing.Clear(); _seen.Clear();
        foreach (var tracked in _plugin.Sync.ServerIdols)
        {
            var idol = tracked.Idol;
            if (!Definition(idol, out var definition) || !ServerItem(idol)) continue;
            if (_outgoing.Count >= MaximumInstances) throw new InvalidOperationException("Shared visual instance capacity exceeded.");
            _seen.Add(idol.netId);
            var holder = Holder(idol);
            var peer = holder?.PlayerMovement.connectionToClient;
            if (!Eligible(peer)) holder = null;
            var holderId = holder != null ? holder.netId : 0;
            var value = 0f;
            if (_published.TryGetValue(idol.netId, out var state) && state.Idol == idol
                && (definition.Authority == IdolVisualAuthority.Server
                    || holderId != 0 && state.Holder == holderId && state.Connection == peer!.Pointer && now - state.Updated <= HolderTimeout))
                value = state.Value;
            else if (definition.Authority == IdolVisualAuthority.Holder) _published.Remove(idol.netId);
            _outgoing.Add(new Entry(idol.netId, idol.SoItem.Id, holderId, value));
        }
        _removed.Clear();
        foreach (var id in _published.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _published.Remove(id);
        _removedPeers.Clear();
        foreach (var pair in _peers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var peer) || peer.Pointer != pair.Value.Connection || !Eligible(peer))
                _removedPeers.Add(pair.Key);
        foreach (var id in _removedPeers) _peers.Remove(id);
        var writer = NetworkWriterPool.Get();
        try
        {
            Header(writer, SnapshotKind);
            writer.WriteUInt(Next(ref _snapshotSequence));
            writer.WriteUShort((ushort)_outgoing.Count);
            foreach (var entry in _outgoing)
            {
                writer.WriteUInt(entry.Idol); writer.WriteULong(entry.Config);
                writer.WriteUInt(entry.Holder); writer.WriteFloat(entry.Value);
            }
            foreach (var peer in NetworkServer.connections.Values)
                if (Eligible(peer)) peer.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    private void ReceiveSnapshot(NetworkConnection connection, NetworkReader reader, int channel)
    {
        var source = NetworkClient.connection;
        if (_disposed || _plugin.RuntimeFailed || !_plugin.Registry.Ready || !_plugin.Sync.ClientCompatible
            || !NetworkClient.isConnected || !NetworkClient.ready || source == null || !source.isAuthenticated
            || source.Pointer != connection.Pointer || channel != 0 || reader.Remaining < SnapshotHeaderLength
            || reader.Remaining > SnapshotHeaderLength + MaximumInstances * EntryLength || !ReadHeader(reader, SnapshotKind)) return;
        if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
        var sequence = reader.ReadUInt();
        var count = reader.ReadUShort();
        if (!Newer(sequence, _receivedSequence) || count > MaximumInstances || reader.Remaining != count * EntryLength) return;
        _incomingIds.Clear();
        for (var index = 0; index < count; index++)
        {
            var row = new Entry(reader.ReadUInt(), reader.ReadULong(), reader.ReadUInt(), reader.ReadFloat());
            if (row.Idol == 0 || row.Idol == row.Holder || !ValidValue(row.Value) || !_incomingIds.Add(row.Idol)
                || !_plugin.Registry.TryGet(row.Config, out var registered) || registered.Definition.SharedVisual == null) return;
            if (NetworkClient.spawned.TryGetValue(row.Idol, out var known) && known != null
                && known.GetComponent<ScoreTool>()?.SoItem?.Id != row.Config) return;
            if (row.Holder != 0 && NetworkClient.spawned.TryGetValue(row.Holder, out var holder) && holder != null
                && holder.GetComponent<PlayerEntity>() == null) return;
            _incoming[index] = row;
        }
        _client.Clear();
        for (var index = 0; index < count; index++)
        {
            var row = _incoming[index];
            if (NetworkClient.spawned.TryGetValue(row.Idol, out var identity) && identity != null)
            {
                var idol = identity.GetComponent<ScoreTool>();
                if (idol != null) _client[row.Idol] = new ClientRow(idol, row);
            }
        }
        _receivedSequence = sequence;
    }

    private void TickVisuals()
    {
        _seen.Clear();
        foreach (var pair in _client)
        {
            var row = pair.Value;
            if (!Definition(row.Idol, out var definition) || row.Idol.SoItem.Id != row.State.Config
                || !NetworkClient.spawned.TryGetValue(pair.Key, out var identity)
                || identity == null || identity.GetComponent<ScoreTool>() != row.Idol) continue;
            var holder = Holder(row.Idol);
            if (!definition.ShowWhenDropped && (row.State.Holder == 0 || holder == null || holder.netId != row.State.Holder)) continue;
            _seen.Add(pair.Key);
            if (_faulted.TryGetValue(pair.Key, out var fault) && fault == row.Idol.Pointer) continue;
            try
            {
                if (_drawings.TryGetValue(pair.Key, out var drawing) && drawing.Idol != row.Idol)
                { Release(drawing); _drawings.Remove(pair.Key); drawing = null; }
                if (drawing == null)
                    _drawings[pair.Key] = drawing = new Drawing(row.Idol, definition.Create(row.Idol)
                        ?? throw new InvalidOperationException("Shared visual factory returned null."));
                if (drawing.Value != row.State.Value)
                {
                    drawing.Visual.Apply(row.State.Value); drawing.Value = row.State.Value;
                    if (_plugin.DebugTools.Enabled && Time.realtimeSinceStartup >= drawing.NextLog)
                    {
                        drawing.NextLog = Time.realtimeSinceStartup + 2f;
                        _plugin.Log.LogInfo($"[Shared visual] config={row.State.Config}, idol={pair.Key}, holder={row.State.Holder}, value={drawing.Value:F3}.");
                    }
                }
                drawing.Visual.Tick(Time.unscaledDeltaTime);
            }
            catch (Exception error)
            {
                if (_drawings.Remove(pair.Key, out var broken))
                    try { broken.Visual.Dispose(); } catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
                _faulted[pair.Key] = row.Idol.Pointer;
                _plugin.Log.LogError($"Shared visual disabled for idol={pair.Key} until released/destroyed: {error}");
            }
        }
        _removed.Clear();
        foreach (var id in _drawings.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { Release(_drawings[id]); _drawings.Remove(id); }
        _removed.Clear();
        foreach (var id in _faulted.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _faulted.Remove(id);
    }

    private void Header(NetworkWriter writer, byte kind)
    {
        writer.WriteUShort(MessageId); writer.WriteUInt(Magic);
        writer.WriteByte(Protocol); writer.WriteByte(kind); writer.WriteULong(_plugin.Registry.Manifest);
    }
    private bool ReadHeader(NetworkReader reader, byte kind) => reader.ReadUInt() == Magic
        && reader.ReadByte() == Protocol && reader.ReadByte() == kind && reader.ReadULong() == _plugin.Registry.Manifest;

    private void EnsureHandlers()
    {
        if (NetworkServer.handlers.TryGetValue(MessageId, out var server) && server.Pointer != _serverHandler.Pointer)
            throw new InvalidOperationException("Shared visual message ID conflicts with another server handler.");
        if (NetworkClient.handlers.TryGetValue(MessageId, out var client) && client.Pointer != _clientHandler.Pointer)
            throw new InvalidOperationException("Shared visual message ID conflicts with another client handler.");
        if (!NetworkServer.handlers.ContainsKey(MessageId)) NetworkServer.handlers.Add(MessageId, _serverHandler);
        if (!NetworkClient.handlers.ContainsKey(MessageId)) NetworkClient.handlers.Add(MessageId, _clientHandler);
    }

    private void ClearServer()
    {
        _published.Clear(); _peers.Clear(); _outgoing.Clear();
        _snapshotSequence = 0; _nextSnapshot = 0; _serverActive = false;
    }
    private void ClearClient()
    {
        _client.Clear(); _proposal = null; _connection = IntPtr.Zero;
        _requestSequence = _receivedSequence = 0;
        foreach (var drawing in _drawings.Values) Release(drawing);
        _drawings.Clear(); _faulted.Clear();
    }
    private void Release(Drawing drawing)
    {
        try { drawing.Visual.Dispose(); }
        catch (Exception error) { _plugin.Log.LogError($"Shared visual cleanup failed for idol={drawing.Idol.netId}: {error}"); }
    }
    internal void Reset(bool server, bool shutdown)
    {
        if (_disposed) return;
        if (shutdown) { if (server) ClearServer(); else ClearClient(); }
        else EnsureHandlers();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearServer(); ClearClient();
        if (NetworkServer.handlers.TryGetValue(MessageId, out var server) && server.Pointer == _serverHandler.Pointer)
            NetworkServer.handlers.Remove(MessageId);
        if (NetworkClient.handlers.TryGetValue(MessageId, out var client) && client.Pointer == _clientHandler.Pointer)
            NetworkClient.handlers.Remove(MessageId);
    }
}

[HarmonyPatch(typeof(NetworkServer), "RegisterMessageHandlers")]
internal static class VisualServerHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.VisualStates?.Reset(true, false);
}
[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class VisualClientHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.VisualStates?.Reset(false, false);
}
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class VisualServerShutdownPatch
{
    [HarmonyPrefix] private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.VisualStates?.Reset(true, true);
}
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class VisualClientDisconnectPatch
{
    [HarmonyPrefix] private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.VisualStates?.Reset(false, true);
}
