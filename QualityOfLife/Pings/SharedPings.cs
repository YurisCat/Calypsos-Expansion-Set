using Entities.Monster;
using Entities.Player;
using HarmonyLib;
using Items;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using User;
using _200_Dev.Entities;
using _200_Dev.Portrait;
using _200_Dev.Entities.Player.Behaviours.Inventory;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal sealed class SharedPings : IDisposable
{
    internal const ushort MessageId = 0x4D50;
    private sealed record Peer(NetworkConnectionToClient Connection, UserInstance User, PlayerEntity Player)
    { internal uint Sequence; internal double NextRequest; }
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly NetworkMessageDelegate _serverHandler, _clientHandler;
    private readonly PingState _state = new();
    private readonly Dictionary<uint, Peer> _peers = new();
    private readonly PingInput _input;
    private readonly PingView _view;
    private PingEntry[] _rows = Array.Empty<PingEntry>();
    private uint _epoch = 1, _sentSequence, _receivedSequence, _clientEpoch, _requestSequence;
    private IntPtr _connection;
    private double _nextSend, _receivedAt = double.NegativeInfinity;
    private int _scene;
    private bool _disposed, _faulted;

    internal SharedPings(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _serverHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveRequest;
        _clientHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveSnapshot;
        _input = new PingInput(plugin);
        _view = new PingView(this);
        _scene = SceneManager.GetActiveScene().handle;
        try { EnsureHandlers(); }
        catch { Dispose(); throw; }
    }

    internal IReadOnlyList<PingEntry> Rows => _rows;
    internal bool ClientReady => !_disposed && !_faulted && !_plugin.RuntimeFailed && _plugin.Registry.Ready
        && NetworkClient.isConnected && NetworkClient.ready && NetworkClient.connection?.isAuthenticated == true
        && _plugin.Sync.ClientCompatible;

    internal void Tick()
    {
        if (_disposed || _faulted) return;
        try
        {
            EnsureHandlers();
            var scene = SceneManager.GetActiveScene().handle;
            if (scene != _scene) { _scene = scene; ClearRound(); ClearClient(); }
            if (NetworkServer.active && _plugin.Registry.Ready)
            {
                _state.Prune(NetworkTime.time, ServerValid);
                if (NetworkTime.time >= _nextSend)
                { _nextSend = NetworkTime.time + 0.25; SendSnapshot(); }
            }
            else { _state.Entries.Clear(); _peers.Clear(); _nextSend = 0; }
            var source = NetworkClient.connection;
            if (!ClientReady || source == null) ClearClient();
            else if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            if (Time.realtimeSinceStartupAsDouble - _receivedAt > 1.5) _rows = Array.Empty<PingEntry>();
            _input.Tick(this);
            _view.Tick();
        }
        catch (Exception error) { Fault(error); }
    }

    private bool Eligible(NetworkConnectionToClient connection) => connection != null
        && connection.isAuthenticated && connection.isReady && connection.identity != null
        && _plugin.Sync.IsCompatible(connection);

    private bool Resolve(NetworkConnectionToClient connection, out UserInstance user, out PlayerEntity player)
    {
        user = UserInstancesManager.Instance?.Get(connection)!;
        player = user != null ? user.playerEntity : null!;
        return Eligible(connection) && user != null && user.netId != 0 && player != null
            && player.isActiveAndEnabled && player.netId != 0
            && player.PlayerMovement?.connectionToClient?.Pointer == connection.Pointer
            && NetworkServer.spawned.TryGetValue(player.netId, out var spawned) && spawned != null
            && spawned.GetComponent<PlayerEntity>() == player;
    }

    private static int Slot(PlayerEntity player)
    {
        var portraits = PortraitManager.Instance?.Portrait;
        if (portraits == null || portraits.Count > PingState.Capacity) return -1;
        for (var i = 0; i < portraits.Count; i++)
            if (portraits[i] != null && portraits[i].CurrentPortraitOwner == player) return i;
        return -1;
    }

    private bool ServerValid(PingEntry entry)
    {
        if (!_peers.TryGetValue(entry.Owner, out var peer)
            || !NetworkServer.connections.TryGetValue(peer.Connection.connectionId, out var current)
            || current == null || current.Pointer != peer.Connection.Pointer || !Resolve(current, out var user, out var player)
            || user != peer.User || player != peer.Player) return false;
        var slot = Slot(player);
        if (slot != entry.Slot) return false;
        return entry.Target == 0 || ItemFor(entry.Target, true) != null;
    }

    internal static Item? ItemFor(uint netId, bool server = false)
    {
        var table = server ? NetworkServer.spawned : NetworkClient.spawned;
        if (netId == 0 || !table.TryGetValue(netId, out var identity) || identity == null) return null;
        var item = PingItems.Find(identity);
        return item != null && PingItems.Eligible(item) ? item : null;
    }

    internal static Item? HitItem(RaycastHit hit) => hit.collider != null ? hit.collider.GetComponentInParent<Item>() : null;

    internal static bool Raycast(Ray ray, int mask, out RaycastHit hit)
    {
        hit = default;
        var nearest = float.PositiveInfinity;
        var found = false;
        // RaycastAll is unordered. Select the nearest eligible surface without
        // changing physics layers or allowing walls to be skipped.
        var hits = Physics.RaycastAll(ray, float.PositiveInfinity, mask, QueryTriggerInteraction.Collide);
        for (var i = 0; i < hits.Length; i++)
        {
            var candidate = hits[i];
            var collider = candidate.collider;
            if (collider == null || candidate.distance >= nearest) continue;
            var item = HitItem(candidate);
            if (item != null)
            {
                // Corpse tools retain Item targeting even in a character hierarchy.
                if (!PingItems.Eligible(item)) continue;
            }
            else if (collider.isTrigger || collider.GetComponentInParent<PlayerEntity>() != null
                || collider.GetComponentInParent<MonsterEntity>() != null) continue;
            hit = candidate;
            nearest = candidate.distance;
            found = true;
        }
        return found;
    }

    internal static PingPoint Point(Vector3 value) => new(value.x, value.y, value.z);
    internal static Vector3 Vector(PingPoint value) => new(value.X, value.Y, value.Z);

    internal void Request(Ray ray, uint target)
    {
        if (!ClientReady || _clientEpoch == 0 || _receivedSequence == 0) return;
        _requestSequence = Next(_requestSequence);
        var request = new PingRequest(_requestSequence, _clientEpoch, target, Point(ray.origin), Point(ray.direction));
        Send(NetworkClient.connection!, PingWire.Request(request, _plugin.Registry.Manifest));
    }

    internal void CancelOwn()
    {
        var user = UserInstance.Me;
        if (!ClientReady || _clientEpoch == 0 || _receivedSequence == 0 || user == null) return;
        foreach (var row in _rows)
        {
            if (row.Owner != user.netId || row.Expires <= NetworkTime.time) continue;
            _requestSequence = Next(_requestSequence);
            Send(NetworkClient.connection!, PingWire.Cancel(new PingCancel(_requestSequence, _clientEpoch, row.Revision),
                _plugin.Registry.Manifest));
            return;
        }
    }

    private void ReceiveRequest(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var sender = connection.TryCast<NetworkConnectionToClient>();
            if (_disposed || _faulted || !NetworkServer.active || !_plugin.Registry.Ready || channel != 0
                || sender == null || reader.Remaining != PingWire.RequestSize && reader.Remaining != PingWire.CancelSize
                || !Resolve(sender, out var user, out var player)) return;
            var bytes = Read(reader);
            var clearing = bytes.Length == PingWire.CancelSize;
            PingRequest request = default;
            PingCancel cancel = default;
            if (clearing ? !PingWire.TryCancel(bytes, _plugin.Registry.Manifest, out cancel)
                : !PingWire.TryRequest(bytes, _plugin.Registry.Manifest, out request)) return;
            var sequence = clearing ? cancel.Sequence : request.Sequence;
            if ((clearing ? cancel.Epoch : request.Epoch) != _epoch) return;
            if (!_peers.TryGetValue(user.netId, out var peer) || peer.Connection.Pointer != sender.Pointer
                || peer.User != user || peer.Player != player)
                _peers[user.netId] = peer = new Peer(sender, user, player);
            if (!PingState.Newer(sequence, peer.Sequence)) return;
            peer.Sequence = sequence;
            if (NetworkTime.time < peer.NextRequest) return;
            peer.NextRequest = NetworkTime.time + 0.2;
            var slot = Slot(player);
            var interaction = player.PlayerInteraction;
            if (slot < 0 || player.CurrentHealthSync <= 0 || interaction == null
                || _state.Entries.Count >= PingState.Capacity && !_state.Entries.ContainsKey(user.netId)) return;
            if (clearing)
            {
                if (_state.Cancel(user.netId, cancel.Revision)) SendSnapshot();
                return;
            }
            var origin = Vector(request.Origin); var direction = Vector(request.Direction);
            if ((origin - player.transform.position).sqrMagnitude > 25f
                || direction.sqrMagnitude is < 0.99f or > 1.01f) return;
            var mask = (int)interaction._occlusionLayerMask | (int)interaction._interactableLayerMask;
            if (!Raycast(new Ray(origin, direction), mask, out var hit)) return;
            var item = HitItem(hit);
            var target = item != null ? item.netId : 0;
            if (target != request.Target || item != null && (target == 0 || ItemFor(target, true) != item)) return;
            _state.Put(user.netId, target, (byte)slot, Point(hit.point), NetworkTime.time);
            SendSnapshot();
        }
        catch (Exception error) { Fault(error); }
    }

    private void SendSnapshot()
    {
        _state.Prune(NetworkTime.time, ServerValid);
        // Keep replay/rate-limit state while the connection is alive, even
        // after its marker expires, but release departed users promptly.
        foreach (var key in _peers.Keys.ToArray())
        {
            var peer = _peers[key];
            if (!NetworkServer.connections.TryGetValue(peer.Connection.connectionId, out var current)
                || current == null || current.Pointer != peer.Connection.Pointer
                || !Resolve(current, out var user, out var player) || user != peer.User || player != peer.Player)
                _peers.Remove(key);
        }
        _sentSequence = Next(_sentSequence);
        var bytes = PingWire.Snapshot(_state.Entries.Values, _state.Entries.Count, _plugin.Registry.Manifest,
            _sentSequence, _epoch, NetworkTime.time);
        foreach (var connection in NetworkServer.connections.Values)
            if (Eligible(connection)) Send(connection, bytes);
    }

    private void ReceiveSnapshot(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var source = NetworkClient.connection;
            if (!ClientReady || source == null || source.Pointer != connection.Pointer || channel != 0
                || reader.Remaining < PingWire.SnapshotHeader
                || reader.Remaining > PingWire.SnapshotHeader + PingWire.EntrySize * PingState.Capacity) return;
            if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            if (!PingWire.TrySnapshot(Read(reader), _plugin.Registry.Manifest, out var sequence, out var epoch,
                out var sentAt, out var rows) || !PingState.Newer(sequence, _receivedSequence)
                || Math.Abs(NetworkTime.time - sentAt) > 30) return;
            // Validate the entire table before publishing it. Unspawned targets
            // remain pending; later snapshots and normal spawn ordering recover.
            foreach (var row in rows)
            {
                if (NetworkClient.spawned.TryGetValue(row.Owner, out var owner) && owner != null
                    && owner.GetComponent<UserInstance>() == null) return;
                if (row.Target != 0 && NetworkClient.spawned.TryGetValue(row.Target, out var target)
                    && target != null && PingItems.Find(target) == null) return;
            }
            if (_clientEpoch != epoch) _input.Reset();
            _rows = rows; _receivedSequence = sequence; _clientEpoch = epoch;
            _receivedAt = Time.realtimeSinceStartupAsDouble;
        }
        catch (Exception error) { Fault(error); }
    }

    private static byte[] Read(NetworkReader reader)
    {
        var bytes = new byte[reader.Remaining];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = reader.ReadByte();
        return bytes;
    }

    private static void Send(NetworkConnection connection, byte[] bytes)
    {
        var writer = NetworkWriterPool.Get();
        try
        {
            writer.WriteUShort(MessageId);
            foreach (var value in bytes) writer.WriteByte(value);
            connection.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    private void EnsureHandlers()
    {
        Handler(NetworkServer.handlers, _serverHandler);
        Handler(NetworkClient.handlers, _clientHandler);
    }

    private static void Handler(Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate> handlers,
        NetworkMessageDelegate handler)
    {
        if (handlers.TryGetValue(MessageId, out var current))
        {
            if (current.Pointer != handler.Pointer) throw new InvalidOperationException("Shared ping message ID conflict.");
        }
        else handlers.Add(MessageId, handler);
    }

    private static uint Next(uint value) => value == uint.MaxValue ? 1 : value + 1;

    internal void ClearRound()
    {
        if (_disposed || _faulted) return;
        try
        {
            _state.Entries.Clear(); _peers.Clear(); _epoch = Next(_epoch);
            _rows = Array.Empty<PingEntry>(); _clientEpoch = 0;
            _input.Reset();
            if (NetworkServer.active && _plugin.Registry.Ready) SendSnapshot();
            _view.Clear();
        }
        catch (Exception error) { Fault(error); }
    }

    private void ClearClient()
    {
        _rows = Array.Empty<PingEntry>(); _connection = IntPtr.Zero;
        _receivedSequence = _clientEpoch = _requestSequence = 0;
        _receivedAt = double.NegativeInfinity;
        _input.Reset();
        _view.Clear();
    }

    internal void TransportReset(bool shutdown = false)
    {
        if (_disposed || _faulted) return;
        try
        {
            if (shutdown)
            { _state.Entries.Clear(); _peers.Clear(); _epoch = Next(_epoch); _sentSequence = 0; _nextSend = 0; ClearClient(); }
            else EnsureHandlers();
        }
        catch (Exception error) { Fault(error); }
    }

    internal void Capture(Camera camera) => _view.Capture(camera);
    internal void FinishCamera(Camera camera) => _view.FinishCamera(camera);
    internal void CapturePanini(UnityEngine.Rendering.Universal.PostProcessPassRenderGraph.PaniniProjectionPassData data)
        => _view.CapturePanini(data);
    internal void CaptureDistortion(Material material) => _view.CaptureDistortion(material);
    internal void Render(Il2CppSystem.Collections.Generic.List<Camera> cameras) => _view.Render(cameras);

    internal void Trace(string message)
    {
        if (_plugin.DebugTools.Enabled) _plugin.Log.LogInfo("[Shared pings] " + message);
    }

    internal void PickedUp(Item item)
    {
        if (_disposed || _faulted || !NetworkServer.active || item == null || item.ItemOwner == null) return;
        try
        {
            if (_state.Prune(NetworkTime.time, entry => entry.Target != item.netId)) SendSnapshot();
        }
        catch (Exception error) { Fault(error); }
    }

    internal void Fault(Exception error)
    {
        if (_disposed || _faulted) return;
        _faulted = true; _state.Entries.Clear(); _peers.Clear(); ClearClient();
        _input.Dispose(); _view.Dispose();
        _plugin.Log.LogError($"[Shared pings] Disabled after an error; restart to restore marking: {error}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _input.Dispose(); _view.Dispose(); _state.Entries.Clear(); _peers.Clear(); _rows = Array.Empty<PingEntry>();
        if (NetworkServer.handlers.TryGetValue(MessageId, out var server) && server.Pointer == _serverHandler.Pointer)
            NetworkServer.handlers.Remove(MessageId);
        if (NetworkClient.handlers.TryGetValue(MessageId, out var client) && client.Pointer == _clientHandler.Pointer)
            NetworkClient.handlers.Remove(MessageId);
    }
}

[HarmonyPatch(typeof(NetworkServer), "RegisterMessageHandlers")]
internal static class PingServerHandlersPatch
{ private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.Pings?.TransportReset(); }
[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class PingClientHandlersPatch
{ private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.Pings?.TransportReset(); }
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class PingDisconnectPatch
{ private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Pings?.TransportReset(true); }
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class PingShutdownPatch
{ private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Pings?.TransportReset(true); }
[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.SrvHandleZonePreChange))]
internal static class PingRoundPatch
{ private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Pings?.ClearRound(); }
[HarmonyPatch(typeof(VhsNetworkManager), "SrvResetStateP1")]
internal static class PingResetPatch
{ private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.Pings?.ClearRound(); }

[HarmonyPatch(typeof(Item), "set_ItemOwner")]
internal static class PingPickupPatch
{ private static void Postfix(Item __instance) => CalypsoExpansionSetPlugin.Instance?.Pings?.PickedUp(__instance); }
