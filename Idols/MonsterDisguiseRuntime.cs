using CalypsoExpansionSet.Visuals;
using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace CalypsoExpansionSet.Idols;

internal sealed class MonsterDisguiseRuntime : IdolContentRuntime
{
    internal const ushort MessageId = 0x4D4C;
    internal const uint Magic = 0x43455344;
    internal const byte Protocol = 1, SnapshotKind = 1;
    internal const int HeaderLength = 28, EntryLength = 12, MaximumInstances = 256;
    private static MonsterDisguiseRuntime? _instance;
    internal readonly record struct Choice(uint Idol, uint Holder, uint Monster);
    private sealed record ServerRow(ScoreTool Idol, PlayerEntity Holder, uint Monster);
    private readonly Dictionary<uint, ServerRow> _server = new();
    private readonly Dictionary<uint, Choice> _client = new();
    private readonly HashSet<uint> _ids = new(), _holders = new();
    private readonly List<uint> _candidates = new(), _removed = new();
    private readonly Choice[] _incoming = new Choice[MaximumInstances];
    private readonly System.Random _random = new();
    private readonly NetworkMessageDelegate _handler;
    private readonly HeldAppearanceRuntime _appearances;
    private IDisposable? _catalogLease;
    private IntPtr _connection;
    private uint _sentSequence, _receivedSequence;
    private float _nextSend;
    private bool _disposed, _serverActive;

    internal MonsterDisguiseRuntime()
    {
        if (_instance != null) throw new InvalidOperationException("Monster disguise runtime already exists.");
        _handler = (Action<NetworkConnection, NetworkReader, int>)Receive;
        _appearances = new HeldAppearanceRuntime(MonsterDisguiseIdol.ConfigId, CreateAppearance);
        try
        {
            _catalogLease = NativeMonsterCatalog.Acquire();
            EnsureHandler();
            _instance = this;
        }
        catch { Dispose(); throw; }
    }

    private static bool Eligible(NetworkConnectionToClient? peer, CalypsoExpansionSetPlugin plugin) =>
        peer != null && peer.isAuthenticated && peer.isReady && peer.identity != null && plugin.Sync.IsCompatible(peer);

    internal static void Choose(HeldIdolContext context)
    {
        var service = _instance ?? throw new InvalidOperationException("Monster disguise runtime is unavailable.");
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin == null || !NetworkServer.active || !context.GameplayReady
            || !HeldAppearanceRuntime.Held(context.Holder, MonsterDisguiseIdol.ConfigId, out var actual) || actual != context.Idol
            || !Eligible(context.Holder.PlayerMovement?.connectionToClient, plugin)) return;
        if (service._server.TryGetValue(context.Idol.netId, out var existing)
            && existing.Idol == context.Idol && existing.Holder == context.Holder) return;
        if (service._server.Count >= MaximumInstances && !service._server.ContainsKey(context.Idol.netId))
            throw new InvalidOperationException("Monster disguise state capacity exceeded.");
        NativeMonsterCatalog.Current.Eligible(service._candidates);
        if (service._candidates.Count == 0)
            throw new InvalidOperationException("No native monster candidate or crying mannequin fallback is loaded.");
        var choice = service._candidates[service._random.Next(service._candidates.Count)];
        service._server[context.Idol.netId] = new ServerRow(context.Idol, context.Holder, choice);
        service._nextSend = 0;
        if (plugin.DebugTools.Enabled)
            plugin.Log.LogInfo($"[Monster disguise] idol={context.Idol.netId}, holder={context.Holder.netId}, asset={choice}, candidates={service._candidates.Count}.");
    }

    internal static void Release(HeldIdolContext context)
    {
        var service = _instance;
        if (service == null || context.Idol == null) return;
        if (service._server.TryGetValue(context.Idol.netId, out var row)
            && row.Idol == context.Idol && row.Holder == context.Holder)
        { service._server.Remove(context.Idol.netId); service._nextSend = 0; }
    }

    public override void Tick()
    {
        if (_disposed) return;
        EnsureHandler();
        NativeMonsterCatalog.Current.Tick();
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin == null || plugin.RuntimeFailed) { Dispose(); return; }
        if (NetworkServer.active && plugin.Registry.Ready)
        {
            _serverActive = true;
            _removed.Clear();
            foreach (var pair in _server)
            {
                var row = pair.Value;
                if (!HeldAppearanceRuntime.Held(row.Holder, MonsterDisguiseIdol.ConfigId, out var idol)
                    || idol != row.Idol || !Eligible(row.Holder.PlayerMovement?.connectionToClient, plugin))
                    _removed.Add(pair.Key);
            }
            foreach (var id in _removed) { _server.Remove(id); _nextSend = 0; }
            if (Time.realtimeSinceStartup >= _nextSend)
            {
                _nextSend = Time.realtimeSinceStartup + 0.25f;
                Send(plugin);
            }
        }
        else if (_serverActive) ClearServer();
        var connection = NetworkClient.connection;
        if (!NetworkClient.isConnected || !NetworkClient.ready || connection == null
            || !connection.isAuthenticated || !plugin.Sync.ClientCompatible)
        { ClearClient(); _appearances.Tick(); return; }
        if (_connection != connection.Pointer) { ClearClient(); _connection = connection.Pointer; }
        _appearances.Tick();
    }

    private void Send(CalypsoExpansionSetPlugin plugin)
    {
        var writer = NetworkWriterPool.Get();
        try
        {
            _sentSequence++; if (_sentSequence == 0) _sentSequence++;
            writer.WriteUShort(MessageId);
            writer.WriteUInt(Magic);
            writer.WriteByte(Protocol);
            writer.WriteByte(SnapshotKind);
            writer.WriteULong(plugin.Registry.Manifest);
            writer.WriteULong(MonsterDisguiseIdol.ConfigId);
            writer.WriteUInt(_sentSequence);
            writer.WriteUShort((ushort)_server.Count);
            foreach (var pair in _server)
            {
                writer.WriteUInt(pair.Key);
                writer.WriteUInt(pair.Value.Holder.netId);
                writer.WriteUInt(pair.Value.Monster);
            }
            foreach (var peer in NetworkServer.connections.Values)
                if (Eligible(peer, plugin)) peer.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    private void Receive(NetworkConnection connection, NetworkReader reader, int channel)
    {
        if (_disposed) return;
        try
        {
            var plugin = CalypsoExpansionSetPlugin.Instance;
            var source = NetworkClient.connection;
            if (plugin == null || plugin.RuntimeFailed || !plugin.Registry.Ready || !plugin.Sync.ClientCompatible
                || !NetworkClient.isConnected || !NetworkClient.ready || source == null || !source.isAuthenticated
                || source.Pointer != connection.Pointer || channel != 0 || reader.Remaining < HeaderLength
                || reader.Remaining > HeaderLength + MaximumInstances * EntryLength
                || reader.ReadUInt() != Magic || reader.ReadByte() != Protocol || reader.ReadByte() != SnapshotKind
                || reader.ReadULong() != plugin.Registry.Manifest || reader.ReadULong() != MonsterDisguiseIdol.ConfigId) return;
            if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            var sequence = reader.ReadUInt();
            var count = reader.ReadUShort();
            if (sequence == 0 || unchecked((int)(sequence - _receivedSequence)) <= 0 || count > MaximumInstances
                || reader.Remaining != count * EntryLength) return;
            _ids.Clear(); _holders.Clear();
            for (var index = 0; index < count; index++)
            {
                var choice = new Choice(reader.ReadUInt(), reader.ReadUInt(), reader.ReadUInt());
                if (choice.Idol == 0 || choice.Holder == 0 || choice.Idol == choice.Holder || choice.Monster == 0
                    || !_ids.Add(choice.Idol) || !_holders.Add(choice.Holder) || NativeMonsterCatalog.Current.Resolve(choice.Monster) == null) return;
                if (NetworkClient.spawned.TryGetValue(choice.Idol, out var known) && known != null
                    && known.GetComponent<ScoreTool>()?.SoItem?.Id != MonsterDisguiseIdol.ConfigId) return;
                if (NetworkClient.spawned.TryGetValue(choice.Holder, out var holder) && holder != null
                    && holder.GetComponent<PlayerEntity>() == null) return;
                _incoming[index] = choice;
            }
            _client.Clear();
            for (var index = 0; index < count; index++)
            {
                var row = _incoming[index];
                if (NetworkClient.spawned.TryGetValue(row.Idol, out var identity) && identity != null)
                    _client[row.Idol] = row;
            }
            _receivedSequence = sequence;
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
    }

    private uint ChoiceFor(PlayerEntity player)
    {
        if (!HeldAppearanceRuntime.Held(player, MonsterDisguiseIdol.ConfigId, out var idol) || idol == null
            || !_client.TryGetValue(idol.netId, out var choice) || choice.Holder != player.netId) return 0;
        return choice.Monster;
    }

    private IPlayerAppearance? CreateAppearance(PlayerEntity player)
    {
        var asset = ChoiceFor(player);
        if (asset == 0) return null;
        var template = NativeMonsterCatalog.Current.Resolve(asset);
        if (template == null) return null;
        var appearance = MonsterPlayerAppearance.Create(player, template);
        return appearance != null ? new ChosenAppearance(this, asset, appearance) : null;
    }

    private sealed class ChosenAppearance : IPlayerAppearance
    {
        private readonly MonsterDisguiseRuntime _owner;
        private readonly uint _asset;
        private readonly IPlayerAppearance _inner;
        internal ChosenAppearance(MonsterDisguiseRuntime owner, uint asset, IPlayerAppearance inner)
        { _owner = owner; _asset = asset; _inner = inner; }
        public bool Matches(PlayerEntity player) => _owner.ChoiceFor(player) == _asset && _inner.Matches(player);
        public void Render() => _inner.Render();
        public void Dispose() => _inner.Dispose();
    }

    private void EnsureHandler()
    {
        if (NetworkServer.handlers.ContainsKey(MessageId)) throw new InvalidOperationException("Disguise snapshot message ID conflicts with a server handler.");
        if (NetworkClient.handlers.TryGetValue(MessageId, out var existing))
        {
            if (existing.Pointer != _handler.Pointer) throw new InvalidOperationException("Disguise snapshot message ID conflicts with a client handler.");
        }
        else NetworkClient.handlers.Add(MessageId, _handler);
    }
    private void ClearServer() { _server.Clear(); _sentSequence = 0; _nextSend = 0; _serverActive = false; }
    private void ClearClient() { _client.Clear(); _receivedSequence = 0; _connection = IntPtr.Zero; }

    internal static void TransportReset(bool server, bool shutdown = false)
    {
        var runtime = _instance;
        if (runtime == null || runtime._disposed) return;
        try
        {
            if (shutdown) { if (server) runtime.ClearServer(); else runtime.ClearClient(); }
            else runtime.EnsureHandler();
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.Fail(error); }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearServer(); ClearClient();
        _appearances.Dispose();
        _catalogLease?.Dispose(); _catalogLease = null;
        if (NetworkClient.handlers.TryGetValue(MessageId, out var current) && current.Pointer == _handler.Pointer)
            NetworkClient.handlers.Remove(MessageId);
        if (_instance == this) _instance = null;
    }
}

[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class DisguiseClientHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => MonsterDisguiseRuntime.TransportReset(false);
}
[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class DisguiseDisconnectPatch
{
    [HarmonyPrefix] private static void Prefix() => MonsterDisguiseRuntime.TransportReset(false, true);
}
[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class DisguiseShutdownPatch
{
    [HarmonyPrefix] private static void Prefix() => MonsterDisguiseRuntime.TransportReset(true, true);
}
