using Entities.Player;
using EnvironmentElements;
using HarmonyLib;
using Mirror;
using UnityEngine;
using User;

namespace CalypsoExpansionSet.QualityOfLife;

// Full current-state snapshots also recover late joins and missed spawn ordering.
// Clients never submit occupancy or shop permissions.
internal sealed class RoomStatusSync : IDisposable
{
    internal const ushort MessageId = 0x4D4E;
    internal const uint Magic = 0x43455352;
    internal const byte Protocol = 1, SnapshotKind = 1, InStartRoom = 1, InShop = 2;
    internal const int HeaderLength = 28, EntryLength = 9, MaximumPlayers = 128;
    internal const float SnapshotInterval = 0.2f, SnapshotLifetime = 1f;
    internal readonly record struct Entry(uint Player, byte Flags, uint Visit);
    private readonly record struct ServerRow(PlayerEntity Player, Room? Shop, Entry State);
    private readonly record struct ClientRow(PlayerEntity Player, Entry State);
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly NetworkMessageDelegate _handler;
    private readonly Dictionary<uint, ServerRow> _server = new();
    private readonly Dictionary<uint, ClientRow> _client = new();
    private readonly HashSet<uint> _seen = new(), _messageIds = new();
    private readonly List<uint> _removed = new();
    private readonly Entry[] _incoming = new Entry[MaximumPlayers];
    private IntPtr _connection;
    private uint _sentSequence, _receivedSequence, _visit;
    private int _startIndex = -1, _shopIndex = -1;
    private float _nextSend, _receivedAt = float.NegativeInfinity;
    private bool _disposed;

    internal RoomStatusSync(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _handler = (Action<NetworkConnection, NetworkReader, int>)Receive;
        try { EnsureHandler(); }
        catch { Dispose(); throw; }
    }

    internal void Tick()
    {
        if (_disposed) return;
        EnsureHandler();
        if (NetworkServer.active && _plugin.Registry.Ready)
        {
            if (Time.realtimeSinceStartup >= _nextSend)
            {
                _nextSend = Time.realtimeSinceStartup + SnapshotInterval;
                UpdateServer();
                SendSnapshot();
            }
        }
        else ClearServer();
        var connection = NetworkClient.connection;
        if (!ClientReady() || connection == null) ClearClient();
        else if (_connection != connection.Pointer)
        {
            ClearClient();
            _connection = connection.Pointer;
        }
    }

    private bool Eligible(NetworkConnectionToClient connection) =>
        connection != null && connection.isAuthenticated && connection.isReady
        && connection.identity != null && _plugin.Sync.IsCompatible(connection);

    private void UpdateServer()
    {
        var start = Room.GetStartRoom();
        var shop = RoomAreas.ActiveShop();
        _seen.Clear();
        var users = UserInstancesManager.Instance;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (!Eligible(connection)) continue;
            var user = users != null ? users.Get(connection) : null;
            var player = user != null ? user.playerEntity : connection.identity?.GetComponent<PlayerEntity>();
            if (player == null || !player.isActiveAndEnabled || player.netId == 0
                || player.PlayerMovement?.connectionToClient?.Pointer != connection.Pointer
                || !NetworkServer.spawned.TryGetValue(player.netId, out var identity)
                || identity == null || identity.GetComponent<PlayerEntity>() != player) continue;
            if (!_seen.Add(player.netId)) throw new InvalidOperationException("Duplicate player in room status.");
            if (_seen.Count > MaximumPlayers) throw new InvalidOperationException("Room status player capacity exceeded.");
            var position = player.transform.position;
            // Death already has its native skull portrait. Occupancy has only
            // two colors; the shop benefit requires a living character.
            byte flags = RoomAreas.Contains(start, position) ? InStartRoom : (byte)0;
            if (player.CurrentHealthSync > 0 && RoomAreas.Contains(shop, position)) flags |= InShop;
            var existed = _server.TryGetValue(player.netId, out var previous);
            var visit = existed ? previous.State.Visit : 0;
            if ((flags & InShop) != 0 && (!existed || previous.Player != player
                || previous.Shop != shop || (previous.State.Flags & InShop) == 0))
            {
                _visit++;
                if (_visit == 0) _visit++;
                visit = _visit;
            }
            _server[player.netId] = new ServerRow(player, shop, new Entry(player.netId, flags, visit));
        }
        _removed.Clear();
        foreach (var key in _server.Keys) if (!_seen.Contains(key)) _removed.Add(key);
        foreach (var key in _removed) _server.Remove(key);
    }

    private void SendSnapshot()
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
            writer.WriteULong(_plugin.Registry.Manifest);
            writer.WriteUInt(_sentSequence);
            writer.WriteInt(Room.GetStartRoom()?.index ?? -1);
            writer.WriteInt(RoomAreas.ActiveShop()?.index ?? -1);
            writer.WriteUShort((ushort)_server.Count);
            foreach (var row in _server.Values)
            {
                writer.WriteUInt(row.State.Player);
                writer.WriteByte(row.State.Flags);
                writer.WriteUInt(row.State.Visit);
            }
            foreach (var connection in NetworkServer.connections.Values)
                if (Eligible(connection)) connection.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    private bool ClientReady() => !_disposed && !_plugin.RuntimeFailed && _plugin.Registry.Ready
        && NetworkClient.isConnected && NetworkClient.ready && _plugin.Sync.ClientCompatible
        && NetworkClient.connection?.isAuthenticated == true;

    internal bool TryGet(PlayerEntity player, out Entry state)
    {
        state = default;
        if (!ClientReady() || player == null || !player.isActiveAndEnabled
            || _connection != NetworkClient.connection!.Pointer
            || Time.realtimeSinceStartup - _receivedAt > SnapshotLifetime
            || !_client.TryGetValue(player.netId, out var row) || row.Player != player
            || !NetworkClient.spawned.TryGetValue(player.netId, out var identity)
            || identity == null || identity.GetComponent<PlayerEntity>() != player) return false;
        state = row.State;
        return true;
    }

    internal bool InCurrentStartRoom(PlayerEntity player, Room start, out bool inside)
    {
        inside = false;
        if (start.index != _startIndex || !TryGet(player, out var state)) return false;
        inside = (state.Flags & InStartRoom) != 0;
        return true;
    }

    internal bool InCurrentShop(PlayerEntity player, Room shop, out uint visit)
    {
        visit = 0;
        if (shop.index != _shopIndex || !TryGet(player, out var state) || (state.Flags & InShop) == 0) return false;
        visit = state.Visit;
        return visit != 0;
    }

    private void Receive(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var source = NetworkClient.connection;
            if (!ClientReady() || source == null || source.Pointer != connection.Pointer || channel != 0
                || reader.Remaining < HeaderLength || reader.Remaining > HeaderLength + MaximumPlayers * EntryLength
                || reader.ReadUInt() != Magic || reader.ReadByte() != Protocol || reader.ReadByte() != SnapshotKind
                || reader.ReadULong() != _plugin.Registry.Manifest) return;
            if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            var sequence = reader.ReadUInt();
            var startIndex = reader.ReadInt();
            var shopIndex = reader.ReadInt();
            var count = reader.ReadUShort();
            if (!Newer(sequence, _receivedSequence) || startIndex < -1 || shopIndex < -1
                || count > MaximumPlayers || reader.Remaining != count * EntryLength) return;
            _messageIds.Clear();
            for (var index = 0; index < count; index++)
            {
                var entry = new Entry(reader.ReadUInt(), reader.ReadByte(), reader.ReadUInt());
                if (entry.Player == 0 || (entry.Flags & ~(InStartRoom | InShop)) != 0
                    || (entry.Flags & InShop) != 0 && (entry.Visit == 0 || shopIndex < 0)
                    || (entry.Flags & InStartRoom) != 0 && startIndex < 0 || !_messageIds.Add(entry.Player)) return;
                if (NetworkClient.spawned.TryGetValue(entry.Player, out var identity) && identity != null
                    && identity.GetComponent<PlayerEntity>() == null) return;
                _incoming[index] = entry;
            }
            _client.Clear();
            for (var index = 0; index < count; index++)
            {
                var entry = _incoming[index];
                if (!NetworkClient.spawned.TryGetValue(entry.Player, out var identity) || identity == null) continue;
                var player = identity.GetComponent<PlayerEntity>();
                if (player != null) _client[entry.Player] = new ClientRow(player, entry);
            }
            _startIndex = startIndex;
            _shopIndex = shopIndex;
            _receivedSequence = sequence;
            _receivedAt = Time.realtimeSinceStartup;
        }
        catch (Exception error) { _plugin.Fail(error); }
    }

    internal static bool Newer(uint candidate, uint previous) =>
        candidate != 0 && unchecked((int)(candidate - previous)) > 0;

    private void EnsureHandler()
    {
        if (NetworkServer.handlers.ContainsKey(MessageId))
            throw new InvalidOperationException("Room status ID conflicts with another server handler.");
        if (NetworkClient.handlers.TryGetValue(MessageId, out var current))
        {
            if (current.Pointer != _handler.Pointer)
                throw new InvalidOperationException("Room status ID conflicts with another client handler.");
        }
        else NetworkClient.handlers.Add(MessageId, _handler);
    }

    private void ClearServer()
    {
        _server.Clear();
        _sentSequence = 0;
        _nextSend = 0;
    }

    private void ClearClient()
    {
        _client.Clear();
        _connection = IntPtr.Zero;
        _receivedSequence = 0;
        _receivedAt = float.NegativeInfinity;
        _startIndex = _shopIndex = -1;
    }

    internal void TransportReset(bool shutdown = false)
    {
        if (_disposed) return;
        try
        {
            if (shutdown) { ClearServer(); ClearClient(); _plugin.ShopStamina?.Reset(); }
            else EnsureHandler();
        }
        catch (Exception error) { _plugin.Fail(error); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearServer();
        ClearClient();
        if (NetworkClient.handlers.TryGetValue(MessageId, out var current) && current.Pointer == _handler.Pointer)
            NetworkClient.handlers.Remove(MessageId);
    }
}

[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class RoomStatusHandlersPatch
{
    private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.RoomStatus?.TransportReset();
}
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class RoomStatusDisconnectPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.RoomStatus?.TransportReset(true);
}
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class RoomStatusShutdownPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.RoomStatus?.TransportReset(true);
}
