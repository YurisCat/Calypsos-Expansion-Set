using CalypsoExpansionSet.Visuals;
using CalypsoExpansionSet.Testing;
using Entities.Player;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.BoardGameLogic;
using _200_Dev.UI.Networking;

namespace CalypsoExpansionSet.Curses;

internal sealed class DisguiseCurse : IDisposable
{
    internal const string Key = "identity_disguise";
    // Internal dice-selection marker only. Never sent to a vanilla alteration runner or SyncList.
    internal const EAlterationType DiceMarker = (EAlterationType)0x43455301;
    private const float SnapshotInterval = 0.5f, SnapshotLifetime = 3f;
    private sealed record Peer(IntPtr Connection, ulong Session);
    private readonly record struct DisplaySource(IntPtr Player, IntPtr Rig, IntPtr Body, int Character, IntPtr Held, IntPtr Flashlight);
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly NetworkMessageDelegate _handler;
    private readonly DisguiseStatusIcon _statusIcon;
    private readonly DisguiseArrivalEffects _arrivalEffects;
    private readonly DisguiseRound _round = new();
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly List<int> _departedPeers = new();
    private readonly List<DisguiseParticipant> _players = new();
    private readonly List<DisguiseEntry> _outgoing = new();
    private readonly Dictionary<uint, DisguiseEntry> _incoming = new();
    private readonly Dictionary<uint, PlayerDisguiseAppearance> _views = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _removed = new();
    private readonly Dictionary<uint, DisplaySource> _faulted = new();
    private IntPtr _connection;
    private ulong _session;
    private uint _sentSequence, _receivedSequence, _receivedRevision;
    private float _nextSend, _receivedAt = float.NegativeInfinity;
    private bool _disposed, _receivedActive, _noticePending;
    internal DisguiseHouseRule HouseRule { get; }
    internal bool Active => _round.Active;

    internal DisguiseCurse(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _statusIcon = new DisguiseStatusIcon(plugin);
        _arrivalEffects = new DisguiseArrivalEffects(this, plugin);
        HouseRule = new DisguiseHouseRule(this, plugin);
        _handler = (Action<NetworkConnection, NetworkReader, int>)Receive;
        try { EnsureHandler(); }
        catch { Dispose(); throw; }
    }

    private bool Eligible(NetworkConnectionToClient connection) => connection != null
        && connection.isAuthenticated && connection.isReady && connection.identity != null
        && _plugin.Sync.IsCompatible(connection);

    private bool ServerReady => !_disposed && !_plugin.RuntimeFailed && NetworkServer.active
        && _plugin.Registry.Ready && _plugin.Sync.AllReadyPeersCompatible;
    private bool ClientReady => !_disposed && !_plugin.RuntimeFailed && _plugin.Registry.Ready
        && NetworkClient.isConnected && NetworkClient.ready && _plugin.Sync.ClientCompatible
        && NetworkClient.connection?.isAuthenticated == true;

    internal void Tick()
    {
        if (_disposed) return;
        EnsureHandler();
        HouseRule.Tick();
        if (!NetworkServer.active)
        {
            if (_round.Active || _peers.Count != 0) ResetServer();
        }
        else if (_plugin.Registry.Ready && Time.realtimeSinceStartup >= _nextSend)
        {
            _nextSend = Time.realtimeSinceStartup + SnapshotInterval;
            GatherPlayers();
            _round.Snapshot(_players, _outgoing);
            SendSnapshot();
        }
        if (!ClientReady || NetworkClient.connection == null) ClearClient();
        else if (_connection != NetworkClient.connection.Pointer)
        {
            ClearClient();
            _connection = NetworkClient.connection.Pointer;
        }
        UpdateViews();
        _arrivalEffects.Tick(HasActiveSnapshot);
        _statusIcon.Update(UserInstance.Me?.playerEntity?.PlayerAlterationUI, HasActiveSnapshot);
        ShowAcquisitionNotice();
    }

    internal bool CanRoll()
    {
        if (!ServerReady) return false;
        GatherPlayers();
        return _players.Count is >= 2 and <= DisguiseRound.MaximumPlayers;
    }

    internal bool ApplyDice(out string reason)
    {
        if (!_round.Active) return Start(out reason);
        reason = "";
        if (!CanRoll())
        {
            reason = "Disguise needs at least two compatible, spawned players.";
            return false;
        }
        // Vanilla may roll an existing rule's alteration, but refuses to add
        // that alteration twice. Keep our mapping and rule lifetime as well.
        _round.Snapshot(_players, _outgoing);
        SendSnapshot(playArrival: true);
        _plugin.Log.LogInfo($"[Disguise dice] already active; kept current assignments: players={_outgoing.Count}, revision={_round.Revision}.");
        return true;
    }

    internal bool Start() => Start(out _);

    internal bool Start(out string reason, bool playArrival = true)
    {
        reason = "";
        if (!ServerReady)
            reason = "Disguise is not ready: the host, content registry and all ready peers must be compatible.";
        else if (_round.Active)
            reason = "Disguise is already active. Use 'curse clear' before applying it again.";
        else
        {
            GatherPlayers();
            if (_players.Count is < 2 or > DisguiseRound.MaximumPlayers)
                reason = $"Disguise found {_players.Count} eligible players in {NetworkServer.connections.Count} connections; needs 2-{DisguiseRound.MaximumPlayers} compatible, spawned players.";
            else if (!_round.Start(_players, max => UnityEngine.Random.Range(0, max)))
                reason = "Disguise could not assign players: duplicate or invalid player identities.";
        }
        if (reason.Length != 0) return false;
        _round.Snapshot(_players, _outgoing);
        SendSnapshot(playArrival);
        _plugin.Log.LogInfo($"[Disguise curse] started: players={_outgoing.Count}, revision={_round.Revision}.");
        return true;
    }

    private void GatherPlayers()
    {
        _players.Clear();
        _departedPeers.Clear();
        foreach (var pair in _peers)
            if (!NetworkServer.connections.TryGetValue(pair.Key, out var connection)
                || connection == null || connection.Pointer != pair.Value.Connection) _departedPeers.Add(pair.Key);
        foreach (var id in _departedPeers) _peers.Remove(id);
        var users = UserInstancesManager.Instance;
        if (users == null) return;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (!Eligible(connection)) continue;
            var user = users.Get(connection);
            var player = user != null ? user.playerEntity : null;
            if (user == null || user.netId == 0 || player == null || player.netId == 0
                || player.PlayerMovement?.connectionToClient?.Pointer != connection.Pointer
                || !NetworkServer.spawned.TryGetValue(player.netId, out var identity)
                || identity == null || identity.GetComponent<PlayerEntity>() != player) continue;
            if (!_peers.TryGetValue(connection.connectionId, out var peer) || peer.Connection != connection.Pointer)
                _peers[connection.connectionId] = peer = new Peer(connection.Pointer, ++_session);
            var character = (int)player.PlayerCharacter;
            if (character is < 0 or > 7) continue;
            var rig = player.PlayerWorldRigController;
            // A swap may synchronize the character before the new rig is attached.
            if (rig == null || (int)rig._clothCharacter != character) continue;
            var cloth = (int)rig._clothColor;
            if (cloth is < 0 or > 8) continue;
            byte headgear = 0;
            var gears = rig?.HeadGearGearConfig?.headConfigData;
            if (gears != null)
                foreach (var gear in gears)
                    if (gear != null && gear.headGearPrefab != null && gear.headGearPrefab.activeSelf
                        && (int)gear.headConfigType is >= 1 and <= 2)
                        headgear |= (byte)(1 << (int)gear.headConfigType);
            // Explicit local test windows share Steam's account, but control separate players.
            var carrier = DisguiseCarrier.ForConnection(user.SteamUserId, peer.Session, LocalMultiplayer.IsActive);
            _players.Add(new DisguiseParticipant(carrier, user.netId, player.netId,
                new DisguiseLook((byte)character, (byte)cloth, headgear, DisguiseProtocol.LimitNick(player.PlayerNick ?? ""))));
        }
    }

    internal void ClearRound()
    {
        HouseRule.EndRound();
        if (_disposed || !NetworkServer.active || !_round.Active) return;
        _round.Clear();
        _outgoing.Clear();
        _incoming.Clear();
        _receivedActive = false;
        _noticePending = false;
        _receivedAt = float.NegativeInfinity;
        _statusIcon.Clear();
        _arrivalEffects.Clear();
        ClearViews();
        SendSnapshot();
        _plugin.Log.LogInfo($"[Disguise curse] cleared: revision={_round.Revision}.");
    }

    private void SendSnapshot(bool playArrival = false)
    {
        if (_disposed || _plugin.RuntimeFailed || !NetworkServer.active || !_plugin.Registry.Ready) return;
        _sentSequence++;
        if (_sentSequence == 0) _sentSequence++;
        var packet = DisguiseProtocol.Encode(_plugin.Registry.Manifest, _sentSequence, _round.Revision,
            _round.Active, _outgoing, playArrival);
        var writer = NetworkWriterPool.Get();
        try
        {
            writer.WriteUShort(DisguiseProtocol.MessageId);
            writer.WriteBytes(packet, 0, packet.Length);
            foreach (var connection in NetworkServer.connections.Values)
                if (Eligible(connection)) connection.Send(writer.ToArraySegment(), 0);
        }
        finally { writer.Dispose(); }
    }

    private void Receive(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var source = NetworkClient.connection;
            if (!ClientReady || source == null || source.Pointer != connection.Pointer || channel != 0
                || reader.Remaining < DisguiseProtocol.HeaderLength || reader.Remaining > DisguiseProtocol.MaximumLength) return;
            if (_connection != source.Pointer) { ClearClient(); _connection = source.Pointer; }
            if (!DisguiseProtocol.TryDecode(reader.ReadBytes(reader.Remaining), _plugin.Registry.Manifest,
                    _receivedSequence, _receivedRevision, out var snapshot)) return;
            foreach (var entry in snapshot!.Entries)
            {
                if (NetworkClient.spawned.TryGetValue(entry.User, out var userIdentity) && userIdentity != null
                    && userIdentity.GetComponent<UserInstance>() == null) return;
                if (NetworkClient.spawned.TryGetValue(entry.Player, out var playerIdentity) && playerIdentity != null
                    && playerIdentity.GetComponent<PlayerEntity>() == null) return;
            }
            if (_receivedRevision != snapshot.Revision) { ClearViews(); _faulted.Clear(); }
            _incoming.Clear();
            foreach (var entry in snapshot.Entries) _incoming.Add(entry.User, entry);
            _receivedSequence = snapshot.Sequence;
            _receivedRevision = snapshot.Revision;
            _receivedAt = Time.realtimeSinceStartup;
            // Acquisition feedback follows the live event, not state restoration
            // or a rule's silent reassignment on later rounds of the same board.
            _noticePending = snapshot.Active && (_noticePending || snapshot.PlayArrival);
            _receivedActive = snapshot.Active;
            // Only the live activation packet carries this event. Periodic and
            // late-join snapshots restore state without replaying old visuals.
            if (snapshot.PlayArrival) _arrivalEffects.Queue(snapshot.Entries);
            else if (!snapshot.Active) _arrivalEffects.Clear();
        }
        catch (Exception error) { _plugin.Fail(error); }
    }

    private bool HasActiveSnapshot => ClientReady && _receivedActive
        && _connection == NetworkClient.connection!.Pointer
        && Time.realtimeSinceStartup - _receivedAt <= SnapshotLifetime;

    private void ShowAcquisitionNotice()
    {
        if (!_noticePending || !HasActiveSnapshot) return;
        var player = UserInstance.Me?.playerEntity;
        if (player == null || !TryLook(player, out _)) return;
        var ui = UIManager.Instance;
        if (ui == null || !ui.isActiveAndEnabled || ui.messageUi == null) return;
        _noticePending = false;
        try
        {
            var duration = BoardGameManager.Instance?.CurseManager?.messageDuration ?? 4f;
            if (!float.IsFinite(duration) || duration <= 0) duration = 4f;
            ui.CliEnqueueMessage(HouseRule.GetDescription(), duration);
        }
        catch (Exception error)
        {
            _plugin.Log.LogWarning($"Disguise acquisition message could not be queued: {error}");
        }
    }

    internal bool TryLook(PlayerEntity player, out DisguiseLook look)
    {
        look = default;
        if (!HasActiveSnapshot || player == null) return false;
        var user = UserInstancesManager.Instance?.Get(player);
        if (user == null || user.playerEntity != player || !_incoming.TryGetValue(user.netId, out var entry)
            || entry.Player != player.netId
            || !NetworkClient.spawned.TryGetValue(entry.Player, out var identity) || identity == null
            || identity.GetComponent<PlayerEntity>() != player) return false;
        look = entry.Look;
        return true;
    }

    internal PlayerDisguiseAppearance? CreateAppearance(PlayerEntity player, Vector3 stretch, float headScale) =>
        TryLook(player, out var look) ? PlayerDisguiseAppearance.Create(this, player, look, stretch, headScale) : null;

    private void UpdateViews()
    {
        PlayerDisguiseAppearance.EndRendering();
        _seen.Clear();
        foreach (var entry in _incoming.Values)
        {
            var user = UserInstancesManager.Instance?.Get(entry.User);
            var player = user != null ? user.playerEntity : null;
            if (player == null || player.CurrentHealthSync <= 0 || !TryLook(player, out var look)) continue;
            _seen.Add(player.netId);
            if (_views.TryGetValue(player.netId, out var current) && !current.Matches(player))
            { current.Dispose(); _views.Remove(player.netId); }
            if (_views.ContainsKey(player.netId)) continue;
            var rig = player.WorldRigInstance;
            var body = player.PlayerWorldRigController?.SkinnedMeshRenderer;
            var source = new DisplaySource(player.Pointer, rig != null ? rig.Pointer : IntPtr.Zero,
                body != null ? body.Pointer : IntPtr.Zero, (int)player.PlayerCharacter,
                player.InventoryNew?.InHand?.Pointer ?? IntPtr.Zero, player.FlashLight?.Pointer ?? IntPtr.Zero);
            if (_faulted.TryGetValue(player.netId, out var failed))
            {
                if (failed == source) continue;
                _faulted.Remove(player.netId);
            }
            try
            {
                var view = PlayerDisguiseAppearance.Create(this, player, look, Vector3.one, 1f);
                if (view != null)
                {
                    _views.Add(player.netId, view);
                    if (_plugin.DebugTools.Enabled)
                        _plugin.Log.LogInfo($"[Disguise display] created: player={player.netId}, character={(int)player.PlayerCharacter}->{look.Character}.");
                }
            }
            catch (Exception error)
            {
                _faulted[player.netId] = source;
                _plugin.Log.LogError($"Disguise display disabled for player={player.netId}: {error}");
            }
        }
        _removed.Clear();
        foreach (var id in _views.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { _views[id].Dispose(); _views.Remove(id); }
        // Death or a removed assignment also releases the failed source, allowing a later retry.
        _removed.Clear();
        foreach (var id in _faulted.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _faulted.Remove(id);
    }

    internal void Render()
    {
        if (!ClientReady) return;
        foreach (var view in _views.Values)
        {
            try
            {
                view.RenderNickname();
                if (!HeldAppearanceRuntime.HasAppearance(view.Player)) view.Render();
                else view.HideCopy();
            }
            catch (Exception error) { _plugin.Fail(error); return; }
        }
    }

    private void ClearViews()
    {
        foreach (var view in _views.Values) view.Dispose();
        _views.Clear();
    }

    private void ClearClient()
    {
        PlayerDisguiseAppearance.EndRendering();
        ClearViews();
        _incoming.Clear();
        _faulted.Clear();
        _receivedSequence = _receivedRevision = 0;
        _receivedAt = float.NegativeInfinity;
        _connection = IntPtr.Zero;
        _receivedActive = false;
        _noticePending = false;
        _statusIcon.Clear();
        _arrivalEffects.Clear();
    }

    private void ResetServer()
    {
        HouseRule.Reset();
        _round.Clear();
        _players.Clear(); _outgoing.Clear(); _peers.Clear(); _departedPeers.Clear();
        _sentSequence = 0;
        _nextSend = 0;
    }

    private void EnsureHandler()
    {
        if (NetworkServer.handlers.ContainsKey(DisguiseProtocol.MessageId))
            throw new InvalidOperationException("Disguise message ID conflicts with a server handler.");
        if (NetworkClient.handlers.TryGetValue(DisguiseProtocol.MessageId, out var current))
        {
            if (current.Pointer != _handler.Pointer) throw new InvalidOperationException("Disguise message ID conflicts with a client handler.");
        }
        else NetworkClient.handlers.Add(DisguiseProtocol.MessageId, _handler);
    }

    internal void TransportReset(bool shutdown = false)
    {
        if (_disposed) return;
        try
        {
            if (shutdown) { ClearClient(); ResetServer(); }
            else EnsureHandler();
        }
        catch (Exception error) { _plugin.Fail(error); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        HouseRule.Dispose();
        ClearClient(); ResetServer();
        _statusIcon.Dispose();
        if (NetworkClient.handlers.TryGetValue(DisguiseProtocol.MessageId, out var current) && current.Pointer == _handler.Pointer)
            NetworkClient.handlers.Remove(DisguiseProtocol.MessageId);
    }
}
