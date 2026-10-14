using CalypsoExpansionSet.Visuals;
using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;
using User;

namespace CalypsoExpansionSet.Idols;

// The type owns one service on every peer. Authoritative fatigue outlives a
// HeldIdolEffect; only currently held local movement consumes its snapshot.
internal sealed class RelayIdolRuntime : IdolContentRuntime
{
    internal const ushort MessageId = 0x4D4B;
    internal const uint Magic = 0x52454C59;
    internal const byte Protocol = 2, SnapshotKind = 1;
    internal const int HeaderLength = 30, EntryLength = 12, MaximumInstances = 256;
    internal const float SnapshotInterval = 0.2f;
    internal static readonly Color FreshColor = new(0.025f, 1f, 0.06f, 1f);
    internal static readonly Color TiredColor = new(1f, 0.025f, 0.015f, 1f);
    private static RelayIdolRuntime? _instance;

    private sealed class ServerState
    {
        internal readonly ScoreTool Idol;
        internal readonly RelayFatigue Fatigue = new();
        internal uint Holder;
        internal ServerState(ScoreTool idol) => Idol = idol;
    }
    private sealed record Peer(IntPtr Connection, long Session);
    internal readonly record struct SnapshotEntry(uint Id, uint Holder, float Fatigue);
    private readonly record struct ClientState(ScoreTool Idol, uint Holder, float Fatigue);

    private readonly Dictionary<uint, ServerState> _server = new();
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly Dictionary<uint, ClientState> _client = new();
    private readonly HashSet<uint> _seen = new(), _messageIds = new();
    private readonly List<uint> _removed = new();
    private readonly List<int> _removedPeers = new();
    private readonly SnapshotEntry[] _incoming = new SnapshotEntry[MaximumInstances];
    private readonly NetworkMessageDelegate _handler;
    private IntPtr _connection;
    private uint _sentSequence, _receivedSequence;
    private long _nextSession;
    private float _lastTime, _nextSend;
    private int _alivePlayers, _clientAlivePlayers;
    private bool _serverWasActive, _disposed;

    internal RelayIdolRuntime()
    {
        if (_instance != null) throw new InvalidOperationException("Relay runtime is already registered.");
        _handler = (Action<NetworkConnection, NetworkReader, int>)Receive;
        try { EnsureHandler(); _instance = this; }
        catch { Dispose(); throw; }
    }

    internal static Color EyeColor(float amount) => Color.Lerp(FreshColor, TiredColor, Mathf.Clamp01(amount));

    public override void Tick()
    {
        if (_disposed) return;
        EnsureHandler();
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin == null || plugin.RuntimeFailed) { Dispose(); return; }
        if (NetworkServer.active && plugin.Registry.Ready) TickServer(plugin);
        else if (!NetworkServer.active && _serverWasActive) ClearServer();
        var connection = NetworkClient.connection;
        if (!NetworkClient.isConnected || !NetworkClient.ready || connection == null
            || !connection.isAuthenticated || !plugin.Sync.ClientCompatible)
        {
            ClearClient();
            return;
        }
        if (_connection != connection.Pointer)
        {
            ClearClient();
            _connection = connection.Pointer;
        }
    }

    private void TickServer(CalypsoExpansionSetPlugin plugin)
    {
        var now = Time.time;
        if (!float.IsFinite(now)) return;
        var elapsed = _serverWasActive ? Math.Max(0f, now - _lastTime) : 0f;
        _lastTime = now;
        _serverWasActive = true;
        var alivePlayers = 0;
        var users = UserInstancesManager.Instance;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (!Eligible(connection, plugin)) continue;
            if (!_peers.TryGetValue(connection.connectionId, out var peer) || peer.Connection != connection.Pointer)
                _peers[connection.connectionId] = new Peer(connection.Pointer, ++_nextSession);
            var user = users != null ? users.Get(connection) : null;
            var player = user != null ? user.playerEntity : connection.identity?.GetComponent<PlayerEntity>();
            if (player != null && player.isActiveAndEnabled && player.CurrentHealthSync > 0
                && player.PlayerMovement != null
                && player.PlayerMovement.connectionToClient?.Pointer == connection.Pointer)
                alivePlayers++;
        }
        _removedPeers.Clear();
        foreach (var pair in _peers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var current)
                || current.Pointer != pair.Value.Connection)
                _removedPeers.Add(pair.Key);
        foreach (var key in _removedPeers) _peers.Remove(key);
        alivePlayers = Math.Clamp(alivePlayers, 0, RelayFatigue.MaximumPlayers);
        if (alivePlayers != _alivePlayers)
        {
            // Keep fatigue across deaths/revivals; switch only the rate and floor.
            _alivePlayers = alivePlayers;
            _nextSend = 0;
            if (plugin.DebugTools.Enabled) plugin.Log.LogInfo($"[Relay] alive players={alivePlayers}; fatigue rules updated.");
        }

        _seen.Clear();
        foreach (var tracked in plugin.Sync.ServerIdols)
        {
            var idol = tracked.Idol;
            if (idol == null || idol.netId == 0 || idol.SoItem == null || idol.SoItem.Id != RelayIdol.ConfigId
                || !NetworkServer.spawned.TryGetValue(idol.netId, out var identity)
                || identity == null || identity.GetComponent<ScoreTool>() != idol) continue;
            var id = idol.netId;
            _seen.Add(id);
            if (!_server.TryGetValue(id, out var row) || row.Idol != idol)
            {
                if (_server.Count >= MaximumInstances && !_server.ContainsKey(id))
                    throw new InvalidOperationException("Relay instance capacity exceeded; state cannot be silently omitted.");
                _server[id] = row = new ServerState(idol);
                _nextSend = 0;
            }
            var holder = idol.ItemOwner?.TryCast<PlayerEntity>();
            var owner = holder != null ? holder.PlayerMovement?.connectionToClient : null;
            if (holder == null || !holder.isActiveAndEnabled || holder.CurrentHealthSync <= 0
                || holder.InventoryNew == null || holder.InventoryNew.InHand != idol
                || owner == null || !Eligible(owner, plugin)
                || !NetworkServer.connections.TryGetValue(owner.connectionId, out var live)
                || live.Pointer != owner.Pointer || !_peers.TryGetValue(owner.connectionId, out var peer))
            {
                if (row.Holder != 0) _nextSend = 0;
                row.Holder = 0;
                row.Fatigue.Release();
                CalypsoExpansionSetApi.PublishVisualState(idol, row.Fatigue.Amount);
                continue;
            }
            var user = UserInstancesManager.Instance != null ? UserInstancesManager.Instance.Get(owner) : null;
            // The game's server-side user instance supplies the account identity.
            // Offline sessions use a monotonic connection lifetime, never a reused
            // numeric connection ID. Respawning a character keeps its carrier.
            var account = user != null ? user.SteamUserId : 0UL;
            var carrier = new RelayCarrier(account, account != 0 ? 0 : peer.Session);
            var continuous = row.Holder == holder.netId && row.Fatigue.Held;
            var reset = row.Fatigue.PickUp(carrier);
            if (!continuous || reset)
            {
                _nextSend = 0;
                if (plugin.DebugTools.Enabled)
                    plugin.Log.LogInfo($"[Relay] idol={id}, holder={holder.netId}, "
                        + $"reset={reset}, fatigue={row.Fatigue.Amount:F3}, alive={_alivePlayers}.");
            }
            // The first sample belongs to the pickup boundary, not the previous
            // frame's holder. Dropped items retain fatigue without accumulating.
            if (continuous && !reset) row.Fatigue.Advance(elapsed, _alivePlayers);
            row.Holder = holder.netId;
            CalypsoExpansionSetApi.PublishVisualState(idol, row.Fatigue.Amount);
        }
        _removed.Clear();
        foreach (var id in _server.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { _server.Remove(id); _nextSend = 0; }
        if (Time.realtimeSinceStartup >= _nextSend)
        {
            _nextSend = Time.realtimeSinceStartup + SnapshotInterval;
            SendSnapshot(plugin);
        }
    }

    private static bool Eligible(NetworkConnectionToClient connection, CalypsoExpansionSetPlugin plugin) =>
        connection != null && connection.isAuthenticated && connection.isReady
        && connection.identity != null && plugin.Sync.IsCompatible(connection);

    private void SendSnapshot(CalypsoExpansionSetPlugin plugin)
    {
        var writer = NetworkWriterPool.Get();
        try
        {
            _sentSequence++;
            if (_sentSequence == 0) _sentSequence++;
            writer.WriteUShort(MessageId);
            writer.WriteUInt(Magic);
            writer.WriteByte(Protocol);
            writer.WriteByte(SnapshotKind);
            writer.WriteULong(plugin.Registry.Manifest);
            writer.WriteULong(RelayIdol.ConfigId);
            writer.WriteUInt(_sentSequence);
            writer.WriteUShort((ushort)_server.Count);
            writer.WriteUShort((ushort)_alivePlayers);
            foreach (var pair in _server)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteUInt(pair.Value.Holder);
                writer.WriteFloat(pair.Value.Fatigue.Amount);
            }
            foreach (var connection in NetworkServer.connections.Values)
                if (Eligible(connection, plugin)) connection.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    internal static bool Newer(uint candidate, uint previous) =>
        candidate != 0 && unchecked((int)(candidate - previous)) > 0;

    private void Receive(NetworkConnection connection, NetworkReader reader, int channel)
    {
        if (_disposed) return;
        try
        {
            var plugin = CalypsoExpansionSetPlugin.Instance;
            var source = NetworkClient.connection;
            if (plugin == null || plugin.RuntimeFailed || !plugin.Registry.Ready || !plugin.Sync.ClientCompatible
                || !NetworkClient.isConnected || !NetworkClient.ready || source == null
                || !source.isAuthenticated || source.Pointer != connection.Pointer || channel != 0
                || reader.Remaining < HeaderLength || reader.Remaining > HeaderLength + MaximumInstances * EntryLength
                || reader.ReadUInt() != Magic || reader.ReadByte() != Protocol || reader.ReadByte() != SnapshotKind
                || reader.ReadULong() != plugin.Registry.Manifest || reader.ReadULong() != RelayIdol.ConfigId) return;
            if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            var sequence = reader.ReadUInt();
            var count = reader.ReadUShort();
            var alivePlayers = reader.ReadUShort();
            if (!Newer(sequence, _receivedSequence) || count > MaximumInstances
                || alivePlayers > RelayFatigue.MaximumPlayers || reader.Remaining != count * EntryLength) return;
            _messageIds.Clear();
            for (var index = 0; index < count; index++)
            {
                var entry = new SnapshotEntry(reader.ReadUInt(), reader.ReadUInt(), reader.ReadFloat());
                if (entry.Id == 0 || entry.Id == entry.Holder || !float.IsFinite(entry.Fatigue)
                    || entry.Fatigue is < 0f or > 1f || !_messageIds.Add(entry.Id)) return;
                // Unknown spawn IDs are never applied or retained. The next full
                // snapshot repairs the normal late-join spawn/message race.
                if (NetworkClient.spawned.TryGetValue(entry.Id, out var identity) && identity != null)
                {
                    var idol = identity.GetComponent<ScoreTool>();
                    if (idol == null || idol.SoItem == null || idol.SoItem.Id != RelayIdol.ConfigId) return;
                }
                _incoming[index] = entry;
            }
            // Commit only after every row has passed structural/type validation.
            _client.Clear();
            for (var index = 0; index < count; index++)
            {
                var entry = _incoming[index];
                if (!NetworkClient.spawned.TryGetValue(entry.Id, out var identity) || identity == null) continue;
                var idol = identity.GetComponent<ScoreTool>();
                if (idol == null || idol.SoItem == null || idol.SoItem.Id != RelayIdol.ConfigId) continue;
                _client[entry.Id] = new ClientState(idol, entry.Holder, entry.Fatigue);
            }
            _clientAlivePlayers = alivePlayers;
            _receivedSequence = sequence;
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
    }

    internal static bool TryGetMultiplier(HeldIdolContext context, out float multiplier)
    {
        multiplier = 1f;
        var service = _instance;
        if (service == null || service._disposed || context.Idol == null || context.Holder == null
            || !service._client.TryGetValue(context.Idol.netId, out var row)
            || row.Idol != context.Idol || row.Holder != context.Holder.netId) return false;
        multiplier = RelayFatigue.Multiplier(row.Fatigue, service._clientAlivePlayers);
        return true;
    }

    private void EnsureHandler()
    {
        if (_disposed) return;
        if (NetworkServer.handlers.ContainsKey(MessageId))
            throw new InvalidOperationException("Relay snapshot ID conflicts with another server handler.");
        if (NetworkClient.handlers.TryGetValue(MessageId, out var current))
        {
            if (current.Pointer != _handler.Pointer)
                throw new InvalidOperationException("Relay snapshot ID conflicts with another client handler.");
        }
        else NetworkClient.handlers.Add(MessageId, _handler);
    }

    private void ClearServer()
    {
        _server.Clear();
        _peers.Clear();
        _serverWasActive = false;
        _sentSequence = 0;
        _lastTime = _nextSend = 0;
        _alivePlayers = 0;
        // _nextSession stays monotonic for this runtime lifetime.
    }

    private void ClearClient()
    {
        _client.Clear();
        _clientAlivePlayers = 0;
        _connection = IntPtr.Zero;
        _receivedSequence = 0;
    }

    internal static void TransportReset(bool server, bool shutdown = false)
    {
        var service = _instance;
        if (service == null || service._disposed) return;
        try
        {
            if (shutdown)
            {
                if (server) service.ClearServer();
                else service.ClearClient();
            }
            else service.EnsureHandler();
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearServer();
        ClearClient();
        if (NetworkClient.handlers.TryGetValue(MessageId, out var current) && current.Pointer == _handler.Pointer)
            NetworkClient.handlers.Remove(MessageId);
        if (_instance == this) _instance = null;
    }
}

[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class RelayClientHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => RelayIdolRuntime.TransportReset(false);
}
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class RelayDisconnectPatch
{
    [HarmonyPrefix] private static void Prefix() => RelayIdolRuntime.TransportReset(false, true);
}
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class RelayShutdownPatch
{
    [HarmonyPrefix] private static void Prefix() => RelayIdolRuntime.TransportReset(true, true);
}
