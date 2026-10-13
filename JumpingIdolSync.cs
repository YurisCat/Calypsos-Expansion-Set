using BepInEx.Logging;
using Mirror;

namespace CalypsoExpansionSet;

// Historical filename; this now handles all registered types. Native prefab/config IDs
// identify each idol, so a second custom instance-marker snapshot is unnecessary.
internal sealed class JumpingIdolSync : IDisposable
{
    private const ushort MessageId = 0x4D49;
    private const uint Magic = 0x4D49444C;
    private const byte ProtocolVersion = 3;
    private const byte Hello = 0;
    private const byte Acknowledge = 1;
    private const byte Reject = 2;
    private const int HeaderSize = 14;
    private const float HandshakeTimeout = 30f;
    internal sealed record TrackedIdol(ScoreTool Idol, bool IsDebug);
    private sealed record WaitingPeer(NetworkConnectionToClient Connection, float Since);

    private readonly ManualLogSource _log;
    private readonly Func<bool> _registryReady;
    private readonly Func<ulong> _manifest;
    private readonly NetworkMessageDelegate _serverHandler;
    private readonly NetworkMessageDelegate _clientHandler;
    private readonly Dictionary<int, NetworkConnectionToClient> _peers = new();
    private readonly Dictionary<int, WaitingPeer> _waiting = new();
    private readonly Dictionary<uint, TrackedIdol> _serverIdols = new();
    private readonly List<int> _removedPeers = new();
    private readonly List<uint> _removedIdols = new();
    private NetworkConnectionToServer? _clientConnection;
    private float _lastHello = float.NegativeInfinity;
    private float _nextPrune;
    private bool _blocked;
    private bool _disposed;
    internal bool ClientCompatible { get; private set; }
    internal int ServerCount => _serverIdols.Count;
    internal int ServerDebugCount => _serverIdols.Values.Count(entry => entry.IsDebug);
    internal IEnumerable<TrackedIdol> ServerIdols => _serverIdols.Values;

    internal bool HasDebugIdol(ulong configId) => _serverIdols.Values.Any(entry => entry.IsDebug
        && entry.Idol != null && entry.Idol.SoItem != null && entry.Idol.SoItem.Id == configId);

    internal bool AllReadyPeersCompatible
    {
        get
        {
            if (_blocked || !_registryReady()) return false;
            foreach (var connection in NetworkServer.connections.Values)
                if (connection.isReady && (!connection.isAuthenticated || !IsCompatible(connection))) return false;
            return true;
        }
    }

    internal JumpingIdolSync(ManualLogSource log, Func<bool> registryReady, Func<ulong> manifest)
    {
        _log = log;
        _registryReady = registryReady;
        _manifest = manifest;
        _serverHandler = (Action<NetworkConnection, NetworkReader, int>)HandleServerMessage;
        _clientHandler = (Action<NetworkConnection, NetworkReader, int>)HandleClientMessage;
        EnsureHandlers();
    }

    internal void Tick()
    {
        if (_disposed) return;
        EnsureHandlers();
        if (!NetworkServer.active)
        {
            _serverIdols.Clear();
            _peers.Clear();
            _waiting.Clear();
        }
        else
        {
            if (UnityEngine.Time.realtimeSinceStartup >= _nextPrune)
            {
                _nextPrune = UnityEngine.Time.realtimeSinceStartup + 1f;
                Prune();
            }
            ServiceWaitingPeers();
        }
        var connection = NetworkClient.connection;
        if (!NetworkClient.isConnected || connection == null || !connection.isAuthenticated)
        {
            _clientConnection = null;
            ClientCompatible = false;
            _lastHello = float.NegativeInfinity;
            return;
        }
        if (_clientConnection == null || _clientConnection.Pointer != connection.Pointer)
        {
            _clientConnection = connection;
            ClientCompatible = false;
            _lastHello = float.NegativeInfinity;
        }
        // Handshake BEFORE ready: late joiners must register assets before native spawn messages.
        if (!_blocked && _registryReady() && !ClientCompatible
            && UnityEngine.Time.realtimeSinceStartup - _lastHello >= 1f)
        {
            _lastHello = UnityEngine.Time.realtimeSinceStartup;
            try { Send(connection, Hello); }
            catch (Exception exception) { _log.LogWarning($"Content handshake send failed; retrying: {exception.Message}"); }
        }
    }

    internal bool AllowReady(NetworkConnectionToClient connection)
    {
        if (_disposed || !NetworkServer.active) return true;
        if (_blocked) { connection.Disconnect(); return false; }
        EnsureHandlers();
        if (!_blocked && _registryReady() && connection.isAuthenticated && IsCompatible(connection)) return true;
        if (!_waiting.TryGetValue(connection.connectionId, out var pending)
            || pending.Connection.Pointer != connection.Pointer)
        {
            _waiting[connection.connectionId] = new WaitingPeer(connection, UnityEngine.Time.realtimeSinceStartup);
            _log.LogInfo($"[Compatibility] holding ready for connectionId={connection.connectionId}; waiting for registry/content handshake.");
        }
        return false;
    }

    internal bool IsCompatible(NetworkConnectionToClient connection) =>
        // The local host uses exactly the same registry and resources as the server.
        connection.TryCast<LocalConnectionToClient>() != null
        || _peers.TryGetValue(connection.connectionId, out var peer) && peer.Pointer == connection.Pointer;

    private void ServiceWaitingPeers()
    {
        _removedPeers.Clear();
        foreach (var pair in _waiting)
        {
            var connection = pair.Value.Connection;
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var current) || current.Pointer != connection.Pointer
                || !_blocked && _registryReady() && connection.isAuthenticated && IsCompatible(connection)
                || UnityEngine.Time.realtimeSinceStartup - pair.Value.Since >= HandshakeTimeout)
                _removedPeers.Add(pair.Key);
        }
        foreach (var id in _removedPeers)
        {
            var pending = _waiting[id];
            _waiting.Remove(id);
            var connection = pending.Connection;
            if (!NetworkServer.connections.TryGetValue(id, out var current) || current.Pointer != connection.Pointer) continue;
            if (!_blocked && _registryReady() && connection.isAuthenticated && IsCompatible(connection))
                NetworkServer.SetClientReady(connection);
            else
            {
                _log.LogWarning($"[Compatibility] disconnecting connectionId={id}: no compatible Calypso's Expansion Set handshake within {HandshakeTimeout}s. Install matching MOD content on every player.");
                connection.Disconnect();
            }
        }
    }

    private void HandleServerMessage(NetworkConnection connection, NetworkReader reader, int channelId)
    {
        try
        {
            var sender = connection.TryCast<NetworkConnectionToClient>();
            if (_disposed || _blocked || !NetworkServer.active || sender == null || !sender.isAuthenticated
                || !NetworkServer.connections.TryGetValue(sender.connectionId, out var current)
                || current.Pointer != sender.Pointer || !_registryReady()) return;
            if (!TryRead(reader, out var version, out var kind, out var manifest) || kind != Hello) return;
            if (version != ProtocolVersion || manifest != _manifest())
            {
                _peers.Remove(sender.connectionId);
                _log.LogWarning($"[Compatibility] rejected connectionId={sender.connectionId}: protocol={version}/{ProtocolVersion}, manifest={manifest:X16}/{_manifest():X16}.");
                Send(sender, Reject);
                sender.Disconnect();
                return;
            }
            var first = !_peers.TryGetValue(sender.connectionId, out var previous) || previous.Pointer != sender.Pointer;
            _peers[sender.connectionId] = sender;
            Send(sender, Acknowledge);
            if (first) _log.LogInfo($"[Compatibility] accepted connectionId={sender.connectionId}, manifest={manifest:X16}.");
            // Resume the original ready operation on the next Update, after this handler returns.
        }
        catch (Exception exception) { _log.LogError($"Server content handshake failed: {exception}"); }
    }

    private void HandleClientMessage(NetworkConnection connection, NetworkReader reader, int channelId)
    {
        try
        {
            var server = NetworkClient.connection;
            if (_disposed || !NetworkClient.isConnected || server == null || !server.isAuthenticated
                || connection.Pointer != server.Pointer || !TryRead(reader, out var version, out var kind, out var manifest)) return;
            if (kind == Reject || kind == Acknowledge && (version != ProtocolVersion || !_registryReady() || manifest != _manifest()))
            {
                ClientCompatible = false;
                _log.LogError("Server content is incompatible. Install the same Calypso's Expansion Set version and registered content on every player.");
                server.Disconnect();
                return;
            }
            if (kind != Acknowledge) return;
            _clientConnection = server;
            ClientCompatible = true;
            _log.LogInfo($"[Compatibility] server acknowledged registered content: {manifest:X16}.");
        }
        catch (Exception exception) { _log.LogError($"Client content handshake failed: {exception}"); }
    }

    private static bool TryRead(NetworkReader reader, out byte version, out byte kind, out ulong manifest)
    {
        version = kind = 0;
        manifest = 0;
        if (reader.Remaining != HeaderSize || reader.ReadUInt() != Magic) return false;
        version = reader.ReadByte();
        kind = reader.ReadByte();
        manifest = reader.ReadULong();
        return true;
    }

    private void Send(NetworkConnection connection, byte kind)
    {
        var writer = NetworkWriterPool.Get();
        try
        {
            writer.WriteUShort(MessageId);
            writer.WriteUInt(Magic);
            writer.WriteByte(ProtocolVersion);
            writer.WriteByte(kind);
            writer.WriteULong(_manifest());
            connection.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    internal void Track(ScoreTool idol, bool debug)
    {
        if (_disposed || !NetworkServer.active || idol == null || idol.netId == 0)
            throw new InvalidOperationException("Only the server can track spawned network idols.");
        if (_serverIdols.TryGetValue(idol.netId, out var existing) && existing.Idol == idol)
        {
            if (debug && !existing.IsDebug) _serverIdols[idol.netId] = existing with { IsDebug = true };
            return;
        }
        _serverIdols[idol.netId] = new TrackedIdol(idol, debug);
    }

    internal void Prune()
    {
        _removedIdols.Clear();
        foreach (var pair in _serverIdols)
            if (pair.Value.Idol == null || pair.Value.Idol.netId != pair.Key || !NetworkServer.spawned.ContainsKey(pair.Key))
                _removedIdols.Add(pair.Key);
        foreach (var id in _removedIdols) _serverIdols.Remove(id);
        _removedPeers.Clear();
        foreach (var pair in _peers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var current) || current.Pointer != pair.Value.Pointer)
                _removedPeers.Add(pair.Key);
        foreach (var id in _removedPeers) _peers.Remove(id);
    }

    private void EnsureHandlers()
    {
        EnsureHandler(NetworkServer.handlers, _serverHandler, "server");
        EnsureHandler(NetworkClient.handlers, _clientHandler, "client");
    }

    private static void EnsureHandler(Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate> handlers,
        NetworkMessageDelegate handler, string side)
    {
        if (handlers.TryGetValue(MessageId, out var current))
        {
            if (current.Pointer != handler.Pointer) throw new InvalidOperationException($"Message ID {MessageId} conflicts with another {side} handler.");
        }
        else handlers.Add(MessageId, handler);
    }

    internal void BlockAdmission()
    {
        _blocked = true;
        ClientCompatible = false;
        var waiting = _waiting.Values.ToArray();
        _waiting.Clear();
        foreach (var peer in waiting)
            try { peer.Connection.Disconnect(); }
            catch (Exception exception) { _log.LogWarning($"Unable to disconnect pending connection: {exception.Message}"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RemoveHandler(NetworkServer.handlers, _serverHandler);
        RemoveHandler(NetworkClient.handlers, _clientHandler);
        _peers.Clear();
        _waiting.Clear();
        _serverIdols.Clear();
        ClientCompatible = false;
    }

    private static void RemoveHandler(Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate> handlers,
        NetworkMessageDelegate handler)
    {
        if (handlers.TryGetValue(MessageId, out var current) && current.Pointer == handler.Pointer) handlers.Remove(MessageId);
    }
}
