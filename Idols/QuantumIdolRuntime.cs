using CalypsoExpansionSet.QualityOfLife;
using Entities.Player;
using EnvironmentElements;
using HarmonyLib;
using Items;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.Entities.Player.Behaviours.Inventory;
using _200_Dev.Utility.Runtime.NetworkManager;
using _200_Dev.FlowLogic;

namespace CalypsoExpansionSet.Idols;

internal sealed class QuantumIdolRuntime : IdolContentRuntime
{
    private sealed class ServerRow
    {
        internal readonly ScoreTool Idol;
        internal readonly QuantumCycle Cycle = new();
        internal uint Revision;
        internal QuantumFlags Flags;
        internal Vector3 Origin;
        internal Quaternion Rotation;
        internal string DebugPhase = "";
        internal double NextDebug;
        internal ServerRow(ScoreTool idol) { Idol = idol; Origin = idol.transform.position; Rotation = idol.transform.rotation; }
    }
    private sealed class Peer
    {
        internal readonly NetworkConnectionToClient Connection;
        internal readonly PlayerEntity? Player;
        internal readonly Dictionary<uint, (QuantumReport Report, double Sampled)> Reports = new();
        internal uint Sequence;
        internal double Sampled = double.NegativeInfinity;
        internal QuantumFrustum Frustum;
        internal bool CameraValid;
        internal Peer(NetworkConnectionToClient connection, PlayerEntity? player) { Connection = connection; Player = player; }
    }
    private sealed class ClientRow
    {
        internal readonly ScoreTool Idol;
        internal QuantumEntry Entry;
        internal QuantumSight Sight;
        internal QuantumSight SentSight;
        internal bool Saw;
        internal double Sampled = double.NegativeInfinity;
        internal ClientRow(ScoreTool idol, QuantumEntry entry) { Idol = idol; Entry = entry; }
    }

    private static QuantumIdolRuntime? _instance;
    private readonly Dictionary<uint, ServerRow> _server = new();
    private readonly Dictionary<uint, ClientRow> _client = new();
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly List<int> _departed = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _removed = new();
    private readonly List<QuantumEntry> _snapshot = new();
    private readonly List<QuantumReport> _reports = new();
    private readonly QuantumView _view = new();
    private readonly QuantumPlacement _placement = new();
    private readonly Func<Bounds, bool> _destinationHidden;
    private readonly NetworkMessageDelegate _serverHandler, _clientHandler;
    private IntPtr _connection;
    private uint _epoch = 1, _clientEpoch, _revision, _sentSequence, _receivedSequence, _reportSequence;
    private double _nextSnapshot, _nextReport, _snapshotAt = double.NegativeInfinity, _transitionUntil, _decisionAt;
    private bool _serverActive, _rosterReady, _disposed, _faulted;

    internal QuantumIdolRuntime()
    {
        if (_instance != null) throw new InvalidOperationException("Quantum runtime is already registered.");
        _destinationHidden = DestinationHidden;
        _serverHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveReport;
        _clientHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveSnapshot;
        try { EnsureHandlers(); _instance = this; }
        catch { Dispose(); throw; }
    }

    private CalypsoExpansionSetPlugin? Plugin => CalypsoExpansionSetPlugin.Instance;
    private bool ClientReady => !_disposed && !_faulted && Plugin?.RuntimeFailed == false && Plugin.Registry.Ready
        && NetworkClient.isConnected && NetworkClient.ready && Plugin.Sync.ClientCompatible
        && NetworkClient.connection?.isAuthenticated == true;
    private bool Eligible(NetworkConnectionToClient connection) => connection != null && connection.isAuthenticated
        && connection.isReady && connection.identity != null && Plugin?.Sync.IsCompatible(connection) == true;
    private static bool IsQuantum(ScoreTool? idol) => idol != null && idol.netId != 0 && idol.SoItem?.Id == QuantumIdol.ConfigId;
    private static PlayerEntity? PlayerFor(NetworkConnectionToClient connection)
    {
        var user = UserInstancesManager.Instance?.Get(connection);
        return user != null ? user.playerEntity : connection.identity?.GetComponent<PlayerEntity>();
    }

    public override void Tick()
    {
        if (_disposed || _faulted) return;
        try
        {
            if (Plugin?.RuntimeFailed != false) return;
            EnsureHandlers();
            if (NetworkServer.active && Plugin.Registry.Ready) TickServer();
            else if (_serverActive) ClearServer();
            if (!ClientReady) ClearClient();
            else
            {
                var source = NetworkClient.connection!;
                if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
                TickClient();
            }
        }
        catch (Exception error) { Fault(error); }
    }

    private void GatherPeers()
    {
        _rosterReady = Plugin!.Sync.AllReadyPeersCompatible;
        var events = GameEventManager.Instance;
        var zone = ZoneManager.Instance;
        if (Flow.Instance != null && Flow.IsPlaying || events?._isCurrentEventLocked == true
            || zone?._shopSpawnPending == true || zone?._clientsNotReady?.Count > 0) _rosterReady = false;
        _departed.Clear();
        foreach (var pair in _peers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var connection)
                || !Eligible(connection) || connection.Pointer != pair.Value.Connection.Pointer) _departed.Add(pair.Key);
        foreach (var id in _departed) _peers.Remove(id);
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (!Eligible(connection)) continue;
            var player = PlayerFor(connection);
            // Dead players still own a real scene camera, often far from their
            // corpse. Do not filter health or require a camera near the corpse.
            if (player == null || player.netId == 0) _rosterReady = false;
            if (!_peers.TryGetValue(connection.connectionId, out var peer) || peer.Connection.Pointer != connection.Pointer
                || peer.Player != player) _peers[connection.connectionId] = new Peer(connection, player);
        }
        if (_peers.Count is <= 0 or > 128) _rosterReady = false;
    }

    private void TickServer()
    {
        _serverActive = true; GatherPeers(); _seen.Clear();
        _placement.BeginFrame();
        var now = NetworkTime.time; var hall = Room.GetStartRoom();
        foreach (var tracked in Plugin!.Sync.ServerIdols)
        {
            var idol = tracked.Idol;
            if (!IsQuantum(idol) || !NetworkServer.spawned.TryGetValue(idol.netId, out var identity)
                || identity == null || identity.GetComponent<ScoreTool>() != idol) continue;
            _seen.Add(idol.netId);
            if (!_server.TryGetValue(idol.netId, out var row) || row.Idol != idol)
            {
                if (_server.Count >= QuantumWire.Capacity && !_server.ContainsKey(idol.netId))
                    throw new InvalidOperationException("Quantum instance capacity exceeded.");
                _server[idol.netId] = row = new ServerRow(idol);
            }
            var position = idol.transform.position;
            var inHall = hall != null && RoomAreas.Contains(hall, position);
            CalypsoExpansionSetApi.PublishVisualState(idol, inHall ? 1f : 0f);
            var hasBounds = QuantumView.BoundsFor(idol, out var bounds);
            var rigidbody = idol.Rb;
            var active = !inHall && hall != null && hall.isActiveAndEnabled && idol.isActiveAndEnabled
                && idol.ItemOwner == null && idol.SnapPoint == null && idol.NetworkTransformBase != null
                && hasBounds && !QuantumPlacement.NearHall(hall, bounds)
                && rigidbody != null && rigidbody.linearVelocity.sqrMagnitude < 0.04f
                && rigidbody.angularVelocity.sqrMagnitude < 0.04f && now >= _transitionUntil;
            var flags = inHall ? QuantumFlags.InHall : active ? QuantumFlags.Active : QuantumFlags.None;
            if (row.Revision == 0 || row.Flags != flags || (position - row.Origin).sqrMagnitude > 0.04f
                || Quaternion.Angle(row.Rotation, idol.transform.rotation) > 3f)
            {
                row.Revision = _revision = QuantumWire.Next(_revision); row.Flags = flags;
                row.Origin = position; row.Rotation = idol.transform.rotation;
                row.Cycle.Reset(); _nextSnapshot = 0;
            }
            if (!active || Time.timeScale <= 0)
            { row.Cycle.Reset(); Trace(row, "inactive"); continue; }
            var observation = Observation(row, now);
            if (!row.Cycle.Advance(observation, now))
            {
                Trace(row, observation == QuantumObservation.Unknown ? "unknown-observer"
                    : observation == QuantumObservation.Seen ? "watched" : "waiting-first-sight");
                continue;
            }
            var anchor = NearbyPlayer(row);
            if (anchor == null) { Trace(row, "no-nearby-player", true); continue; }
            _decisionAt = now;
            if (!_placement.TryFind(idol, anchor, hall!, _destinationHidden, out var target, out var rotation))
            { Trace(row, "attempt-failed", true); continue; }
            // Search, physical validation and commit share this server update;
            // there is no pending candidate or delayed retry to invalidate.
            Teleport(row, target, rotation); _nextSnapshot = 0;
        }
        _removed.Clear();
        foreach (var id in _server.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { _server.Remove(id); _nextSnapshot = 0; }
        if (now >= _nextSnapshot)
        {
            _nextSnapshot = now + 0.15;
            _snapshot.Clear();
            foreach (var pair in _server)
            {
                var r = pair.Value;
                _snapshot.Add(new(pair.Key, r.Revision, r.Flags, QuantumView.Point(r.Idol.transform.position)));
            }
            var bytes = QuantumWire.Snapshot(_snapshot, Plugin.Registry.Manifest,
                _sentSequence = QuantumWire.Next(_sentSequence), _epoch, now);
            foreach (var connection in NetworkServer.connections.Values) if (Eligible(connection)) Send(connection, bytes);
        }
    }

    private QuantumObservation Observation(ServerRow row, double now)
    {
        var consensus = new QuantumConsensus(_rosterReady, _peers.Count);
        foreach (var peer in _peers.Values)
        {
            if (!peer.CameraValid || !peer.Reports.TryGetValue(row.Idol.netId, out var sample)) { consensus.Missing(); continue; }
            consensus.Add(sample.Report, sample.Sampled, now, row.Revision);
        }
        return consensus.Observation;
    }

    private bool DestinationHidden(Bounds bounds)
    {
        foreach (var peer in _peers.Values)
            if (!peer.CameraValid || !QuantumWire.Fresh(peer.Sampled, _decisionAt)
                || peer.Frustum.MayContain(QuantumView.Point(bounds.center), QuantumView.Point(bounds.extents))) return false;
        return true;
    }

    private PlayerEntity? NearbyPlayer(ServerRow row)
    {
        PlayerEntity? closest = null; var distance = 20f * 20f;
        foreach (var peer in _peers.Values)
        {
            var player = peer.Player;
            if (player == null || !player.isActiveAndEnabled || player.CurrentHealthSync <= 0) continue;
            var candidate = (player.transform.position - row.Origin).sqrMagnitude;
            if (candidate < distance) { distance = candidate; closest = player; }
        }
        return closest;
    }

    private void Trace(ServerRow row, string phase, bool attempt = false)
    {
        if (Plugin?.DebugTools.Enabled != true || !attempt && (row.DebugPhase == phase || NetworkTime.time < row.NextDebug)) return;
        row.DebugPhase = phase; row.NextDebug = NetworkTime.time + 1;
        var observers = string.Join(", ", _peers.Values.Select(peer =>
            peer.Reports.TryGetValue(row.Idol.netId, out var sample)
                ? $"{peer.Player?.netId}:{sample.Report.Sight}/age={NetworkTime.time - sample.Sampled:F2}"
                : $"{peer.Player?.netId}:missing"));
        Plugin.Log.LogInfo($"[Quantum] idol={row.Idol.netId}, phase={phase}, armed={row.Cycle.Armed}, "
            + $"roster={_rosterReady}, placement={_placement.LastFailure}, search={_placement.Attempts}/{_placement.SearchMilliseconds:F2}ms; observers=[{observers}].");
    }

    private void Teleport(ServerRow row, Vector3 destination, Quaternion rotation)
    {
        var idol = row.Idol;
        // Native Mirror teleport resets interpolation and broadcasts the same
        // destination. Keep the existing entity, config, skin and value.
        if (idol.ItemOwner != null || idol.NetworkTransformBase == null) return;
        var rigidbody = idol.Rb;
        if (rigidbody == null) return;
        if (!rigidbody.isKinematic)
        { rigidbody.linearVelocity = Vector3.zero; rigidbody.angularVelocity = Vector3.zero; }
        idol.NetworkTransformBase.ServerTeleport(destination, rotation);
        idol.SrvSetSalvageAnchor(destination);
        row.Origin = idol.transform.position; row.Rotation = idol.transform.rotation;
        row.Revision = _revision = QuantumWire.Next(_revision); row.Cycle.Reset();
        if (Plugin!.DebugTools.Enabled) Plugin.Log.LogInfo($"[Quantum] idol={idol.netId} moved to {destination}; "
            + $"search={_placement.Attempts}/{_placement.SearchMilliseconds:F2}ms; waiting for a new observation.");
    }

    private void TickClient(bool changed = false)
    {
        var now = NetworkTime.time;
        if (_clientEpoch == 0 || now - _snapshotAt > 1 || !changed && now < _nextReport) return;
        var player = UserInstance.Me?.playerEntity;
        if (player == null || player.netId == 0) return;
        _nextReport = now + 0.08; _reports.Clear();
        var viewFresh = _view.Ready && QuantumWire.Fresh(_view.Sampled, now);
        foreach (var pair in _client)
        {
            var row = pair.Value; var sight = QuantumSight.Unknown;
            if (viewFresh && QuantumWire.Fresh(row.Sampled, now) && row.Sight.HasFlag(QuantumSight.Valid))
                sight = row.Sight | (row.Saw ? QuantumSight.Saw : 0);
            _reports.Add(new(pair.Key, row.Entry.Revision, sight));
            row.SentSight = sight & ~QuantumSight.Saw;
        }
        Send(NetworkClient.connection!, QuantumWire.Report(_reports, player.netId, QuantumView.Point(_view.Origin),
            viewFresh ? _view.Projection : default,
            Plugin!.Registry.Manifest, _reportSequence = QuantumWire.Next(_reportSequence), _clientEpoch, viewFresh ? _view.Sampled : now));
        foreach (var row in _client.Values) row.Saw = false;
    }

    private void ReceiveSnapshot(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var source = NetworkClient.connection;
            if (!ClientReady || source == null || source.Pointer != connection.Pointer || channel != 0
                || reader.Remaining < QuantumWire.SnapshotHeader
                || reader.Remaining > QuantumWire.SnapshotHeader + QuantumWire.Capacity * QuantumWire.EntrySize) return;
            if (!QuantumWire.TrySnapshot(Read(reader), Plugin!.Registry.Manifest, out var sequence, out var epoch, out var sampled, out var rows)
                || !QuantumWire.Fresh(sampled, NetworkTime.time)
                || _clientEpoch != 0 && (epoch == _clientEpoch ? !QuantumWire.Newer(sequence, _receivedSequence)
                    : !QuantumWire.Newer(epoch, _clientEpoch))) return;
            foreach (var row in rows)
                if (NetworkClient.spawned.TryGetValue(row.Id, out var known) && known != null
                    && !IsQuantum(known.GetComponent<ScoreTool>())) return;
            if (_connection != source.Pointer || _clientEpoch != epoch)
            { ClearClient(); _connection = source.Pointer; }
            _seen.Clear();
            foreach (var entry in rows)
            {
                if (!NetworkClient.spawned.TryGetValue(entry.Id, out var identity) || identity == null) continue;
                var idol = identity.GetComponent<ScoreTool>();
                if (!IsQuantum(idol)) continue;
                _seen.Add(entry.Id);
                if (!_client.TryGetValue(entry.Id, out var row) || row.Idol != idol || row.Entry.Revision != entry.Revision)
                    _client[entry.Id] = row = new ClientRow(idol!, entry);
                row.Entry = entry;
            }
            _removed.Clear(); foreach (var id in _client.Keys) if (!_seen.Contains(id)) _removed.Add(id);
            foreach (var id in _removed) _client.Remove(id);
            _clientEpoch = epoch; _receivedSequence = sequence; _snapshotAt = sampled;
        }
        catch (Exception error) { Fault(error); }
    }

    private void ReceiveReport(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var sender = connection.TryCast<NetworkConnectionToClient>();
            if (_disposed || _faulted || !NetworkServer.active || Plugin?.Registry.Ready != true || sender == null
                || !Eligible(sender) || channel != 0 || reader.Remaining < QuantumWire.ReportHeader
                || reader.Remaining > QuantumWire.ReportHeader + QuantumWire.Capacity * QuantumWire.ReportSize) return;
            if (!QuantumWire.TryReport(Read(reader), Plugin.Registry.Manifest, out var sequence, out var epoch, out var sampled,
                out var playerId, out _, out var camera, out var rows) || epoch != _epoch || !QuantumWire.Fresh(sampled, NetworkTime.time)
                || !_peers.TryGetValue(sender.connectionId, out var peer) || peer.Connection.Pointer != sender.Pointer
                || peer.Player == null || peer.Player != PlayerFor(sender) || peer.Player.netId != playerId
                || !QuantumWire.Newer(sequence, peer.Sequence)) return;
            foreach (var report in rows)
                if (NetworkServer.spawned.TryGetValue(report.Id, out var known) && known != null
                    && !IsQuantum(known.GetComponent<ScoreTool>())) return;
            peer.Sequence = sequence; peer.Sampled = sampled;
            peer.CameraValid = camera.Valid; peer.Frustum = camera.Frustum; peer.Reports.Clear();
            foreach (var report in rows)
            {
                if (!_server.TryGetValue(report.Id, out var row) || row.Revision != report.Revision) continue;
                peer.Reports[report.Id] = (report, sampled);
                if (row.Flags == QuantumFlags.Active && report.Sight.HasFlag(QuantumSight.Saw)) row.Cycle.Saw();
            }
        }
        catch (Exception error) { Fault(error); }
    }

    internal static void Capture(Camera camera)
    {
        var service = _instance;
        if (service?.ClientReady != true) return;
        try { service._view.Begin(camera); }
        catch (Exception error) { service.Fault(error); }
    }
    internal static void Finish(Camera camera)
    {
        var service = _instance;
        if (service?.ClientReady != true) return;
        try
        {
            if (!service._view.Finish(camera)) return;
            var changed = false;
            foreach (var row in service._client.Values)
            {
                row.Sight = QuantumSight.Unknown;
                var entry = row.Entry;
                if (entry.Flags != QuantumFlags.Active || !IsQuantum(row.Idol) || row.Idol.ItemOwner != null
                    || (row.Idol.transform.position - QuantumView.Vector(entry.Position)).sqrMagnitude > 0.25f
                    || !QuantumView.BoundsFor(row.Idol, out var bounds))
                { changed |= row.SentSight != QuantumSight.Unknown; continue; }
                var visible = service._view.Sees(row.Idol, bounds);
                row.Saw |= visible;
                row.Sight = QuantumSight.Valid | (visible ? QuantumSight.Visible : 0);
                row.Sampled = service._view.Sampled;
                changed |= row.Sight != row.SentSight;
            }
            // Visibility edges bypass the periodic heartbeat. A brief look
            // away must not disappear between two 80 ms report intervals.
            service.TickClient(changed);
        }
        catch (Exception error) { service.Fault(error); }
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
        try { writer.WriteUShort(QuantumWire.MessageId); foreach (var value in bytes) writer.WriteByte(value); connection.Send(writer.ToArraySegment(), 0); }
        finally { writer.Dispose(); }
    }
    private void EnsureHandlers()
    {
        SetHandler(NetworkServer.handlers, _serverHandler); SetHandler(NetworkClient.handlers, _clientHandler);
    }
    private static void SetHandler(Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate> handlers, NetworkMessageDelegate handler)
    {
        if (handlers.TryGetValue(QuantumWire.MessageId, out var current))
        { if (current.Pointer != handler.Pointer) throw new InvalidOperationException("Quantum message ID conflicts with another handler."); }
        else handlers.Add(QuantumWire.MessageId, handler);
    }
    private void ClearServer()
    {
        _server.Clear(); _peers.Clear(); _serverActive = false; _rosterReady = false;
        _epoch = QuantumWire.Next(_epoch); _sentSequence = 0; _nextSnapshot = 0;
    }
    private void ClearClient()
    {
        _client.Clear(); _view.Reset(); _connection = IntPtr.Zero;
        _clientEpoch = _receivedSequence = _reportSequence = 0;
        _snapshotAt = double.NegativeInfinity; _nextReport = 0;
    }
    internal static void Reset(bool shutdown = false, bool round = false)
    {
        var service = _instance;
        if (service == null || service._disposed || service._faulted) return;
        try
        {
            if (shutdown || round)
            {
                if (NetworkServer.active) service.ClearServer();
                service.ClearClient();
                service._transitionUntil = NetworkTime.time + 1;
            }
            else service.EnsureHandlers();
        }
        catch (Exception error) { service.Fault(error); }
    }
    private void Fault(Exception error)
    {
        if (_faulted || _disposed) return;
        _faulted = true; ClearServer(); ClearClient();
        Plugin?.Log.LogError($"[Quantum] Observation and teleport disabled after an error; restart to restore: {error}");
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ClearServer(); ClearClient();
        if (NetworkServer.handlers.TryGetValue(QuantumWire.MessageId, out var server) && server.Pointer == _serverHandler.Pointer)
            NetworkServer.handlers.Remove(QuantumWire.MessageId);
        if (NetworkClient.handlers.TryGetValue(QuantumWire.MessageId, out var client) && client.Pointer == _clientHandler.Pointer)
            NetworkClient.handlers.Remove(QuantumWire.MessageId);
        if (_instance == this) _instance = null;
    }
}

[HarmonyPatch(typeof(NetworkServer), "RegisterMessageHandlers")]
internal static class QuantumServerHandlersPatch
{ private static void Postfix() => QuantumIdolRuntime.Reset(); }
[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class QuantumClientHandlersPatch
{ private static void Postfix() => QuantumIdolRuntime.Reset(); }
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class QuantumDisconnectPatch
{ private static void Prefix() => QuantumIdolRuntime.Reset(true); }
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class QuantumShutdownPatch
{ private static void Prefix() => QuantumIdolRuntime.Reset(true); }
[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.SrvHandleZonePreChange))]
internal static class QuantumRoundPatch
{ private static void Prefix() => QuantumIdolRuntime.Reset(round: true); }
[HarmonyPatch(typeof(VhsNetworkManager), "SrvResetStateP1")]
internal static class QuantumRetryPatch
{ private static void Prefix() => QuantumIdolRuntime.Reset(round: true); }
