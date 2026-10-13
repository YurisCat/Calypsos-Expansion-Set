using System.Runtime.InteropServices;
using CalypsoExpansionSet.Visuals;
using Entities.Player;
using HarmonyLib;
using Mirror;
using Settings;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Camera;
using FMOD;
using FMODUnity;

namespace CalypsoExpansionSet.Audio;

// Shared by the two actual sound-bearing types. No network components are added
// to an item. The registry owns each bank through its existing resource lifetime.
internal sealed class HeldIdolAudio : IDisposable
{
    private const ushort MessageId = 0x4D4A;
    private const uint Magic = 0x43455341;
    private const byte Version = 1, Request = 0, Playback = 1;
    private const int RequestLength = 22, PlaybackLength = 39;
    private static HeldIdolAudio? _instance;
    private readonly Dictionary<ulong, Bank> _banks = new();
    private readonly Dictionary<int, Peer> _peers = new();
    private readonly List<int> _removed = new();
    private readonly NetworkMessageDelegate _serverHandler, _clientHandler;
    private readonly System.Random _random = new();
    private readonly Channel[] _channels = new Channel[12];
    private int _sourceIndex;
    private uint _requestSequence, _eventSequence, _receivedSequence;
    private IntPtr _clientConnection;
    private bool _disposed;

    private sealed class Peer
    {
        internal IntPtr Connection;
        internal uint Sequence;
        internal ulong Bank;
        internal uint Idol;
        internal float LastTime = float.NegativeInfinity;
        internal Vector3 Position;
        internal int Clip = -1;
    }

    private sealed class Bank : IDisposable
    {
        internal readonly ulong ConfigId;
        internal readonly string Key;
        internal readonly float Interval, MinDistance;
        internal readonly Sound[] Clips;
        internal readonly Action<HeldIdolContext>? OnServerEmission;
        internal Bank(ulong configId, string key, int expectedCount, float interval, float minDistance,
            Action<HeldIdolContext>? onServerEmission)
        {
            ConfigId = configId; Key = key; Interval = interval; MinDistance = minDistance;
            OnServerEmission = onServerEmission;
            Clips = new Sound[expectedCount];
            try
            {
                using var stream = typeof(HeldIdolAudio).Assembly.GetManifestResourceStream($"CalypsoExpansionSet.Audio.{key}.pcm")
                    ?? throw new InvalidOperationException($"Missing licensed audio bank: {key}.");
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt32() != 0x31415343 || reader.ReadInt32() != 48000 || reader.ReadInt32() != expectedCount)
                    throw new InvalidDataException($"Invalid audio bank header: {key}.");
                for (var index = 0; index < Clips.Length; index++)
                {
                    var count = reader.ReadInt32();
                    if (count is < 480 or > 48000 * 4) throw new InvalidDataException("Invalid audio duration.");
                    var data = reader.ReadBytes(checked(count * 4));
                    if (data.Length != count * 4) throw new EndOfStreamException("Truncated audio bank.");
                    for (var i = 0; i < data.Length; i += 4)
                    {
                        var value = BitConverter.ToSingle(data, i);
                        if (!float.IsFinite(value) || Math.Abs(value) > 1f) throw new InvalidDataException("Invalid PCM sample.");
                    }
                    var info = new CREATESOUNDEXINFO
                    {
                        cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(), length = (uint)data.Length,
                        numchannels = 1, defaultfrequency = 48000, format = SOUND_FORMAT.PCMFLOAT
                    };
                    // OPENMEMORY | CREATESAMPLE copies data before returning. Never
                    // use OPENMEMORY_POINT here: the managed pin is released below.
                    var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
                    try
                    {
                        Check(RuntimeManager.CoreSystem.createSound(pin.AddrOfPinnedObject(),
                            MODE.OPENMEMORY | MODE.OPENRAW | MODE.CREATESAMPLE | MODE.LOOP_OFF
                            | MODE._3D | MODE._3D_WORLDRELATIVE | MODE._3D_LINEARROLLOFF, ref info, out Clips[index]), "createSound");
                    }
                    finally { pin.Free(); }
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected audio bank trailer.");
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { for (var i = 0; i < Clips.Length; i++) if (Clips[i].handle != IntPtr.Zero) { Clips[i].release(); Clips[i] = default; } }
    }

    private sealed class Attachment : IDisposable, IVisualDiagnostics
    {
        private IDisposable? _note;
        private HeldIdolAudio? _owner;
        private readonly Bank _bank;
        internal Attachment(IDisposable note, HeldIdolAudio owner, Bank bank) { _note = note; _owner = owner; _bank = bank; }
        public string SetDiagnosticMode(string mode) => (_note as IVisualDiagnostics)?.SetDiagnosticMode(mode) ?? "No note.";
        public void Dispose()
        {
            _note?.Dispose(); _note = null;
            var owner = _owner; _owner = null;
            if (owner == null) return;
            owner._banks.Remove(_bank.ConfigId); _bank.Dispose();
            if (owner._banks.Count == 0) owner.Dispose();
        }
    }

    internal static IDisposable CreateAttached(GameObject prefab, string? artwork, ulong configId,
        string bankKey, int clips, float minimumInterval, float minimumDistance = 0,
        Action<HeldIdolContext>? onServerEmission = null)
    {
        var note = StickyNoteVisuals.Create(prefab, artwork);
        Bank? bank = null;
        try
        {
            bank = new Bank(configId, bankKey, clips, minimumInterval, minimumDistance, onServerEmission);
            var owner = _instance ??= new HeldIdolAudio();
            if (!owner._banks.TryAdd(configId, bank)) throw new InvalidOperationException("Duplicate audio bank config ID.");
            CalypsoExpansionSetPlugin.Instance?.Log.LogInfo($"[Audio] loaded {clips} clips for {bankKey}.");
            return new Attachment(note, owner, bank);
        }
        catch
        {
            note.Dispose(); bank?.Dispose();
            if (_instance?._banks.Count == 0) _instance.Dispose();
            throw;
        }
    }

    private HeldIdolAudio()
    {
        _serverHandler = (Action<NetworkConnection, NetworkReader, int>)ReceiveRequest;
        _clientHandler = (Action<NetworkConnection, NetworkReader, int>)ReceivePlayback;
        try { EnsureHandlers(); }
        catch { Dispose(); throw; }
    }

    private static void Check(RESULT result, string operation)
    {
        if (result != RESULT.OK) throw new InvalidOperationException($"FMOD {operation}: {result}.");
    }

    internal static void Emit(HeldIdolContext context)
    {
        var service = _instance;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (service == null || service._disposed || plugin == null || plugin.RuntimeFailed || context.Idol == null
            || context.Idol.SoItem == null || !service._banks.ContainsKey(context.Idol.SoItem.Id)
            || !Idols.LocalHeldGuard.Ready(context, context.Idol.SoItem.Id)) return;
        try
        {
            service.EnsureHandlers();
            var connection = NetworkClient.connection;
            if (connection == null) return;
            var writer = NetworkWriterPool.Get();
            try
            {
                service.Header(writer, Request);
                writer.WriteUInt(context.Idol.netId);
                writer.WriteUInt(Next(ref service._requestSequence));
                connection.Send(writer.ToArraySegment(), 0);
            }
            finally { writer.Dispose(); }
        }
        catch (Exception error) { service.Fail(error); }
    }

    private static uint Next(ref uint sequence) { sequence++; if (sequence == 0) sequence++; return sequence; }
    internal static bool Newer(uint candidate, uint previous) => candidate != 0 && unchecked((int)(candidate - previous)) > 0;
    internal static int ChooseClip(System.Random random, int count, int previous)
    {
        if (count < 2) return 0;
        if (previous < 0 || previous >= count) return random.Next(count);
        var value = random.Next(count - 1);
        return value >= previous ? value + 1 : value;
    }
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private bool HeaderValid(NetworkReader reader, byte kind)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        return !_disposed && plugin != null && !plugin.RuntimeFailed && plugin.Registry.Ready
            && reader.Remaining == (kind == Request ? RequestLength : PlaybackLength)
            && reader.ReadUInt() == Magic && reader.ReadByte() == Version && reader.ReadByte() == kind
            && reader.ReadULong() == plugin.Registry.Manifest;
    }
    private void Header(NetworkWriter writer, byte kind)
    {
        writer.WriteUShort(MessageId); writer.WriteUInt(Magic); writer.WriteByte(Version); writer.WriteByte(kind);
        writer.WriteULong(CalypsoExpansionSetPlugin.Instance!.Registry.Manifest);
    }
    private bool Resolve(bool server, uint id, out ScoreTool idol, out PlayerEntity holder, out Bank bank)
    {
        idol = null!; holder = null!; bank = null!;
        var spawned = server ? NetworkServer.spawned : NetworkClient.spawned;
        if (id == 0 || !spawned.TryGetValue(id, out var identity) || identity == null) return false;
        idol = identity.GetComponent<ScoreTool>();
        if (idol == null || idol.SoItem == null || !_banks.TryGetValue(idol.SoItem.Id, out bank!)) return false;
        holder = idol.ItemOwner?.TryCast<PlayerEntity>()!;
        return holder != null && holder.isActiveAndEnabled && holder.CurrentHealthSync > 0
            && holder.PlayerMovement != null && holder.InventoryNew != null && holder.InventoryNew.InHand == idol;
    }
    private void ReceiveRequest(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var plugin = CalypsoExpansionSetPlugin.Instance;
            var sender = connection.TryCast<NetworkConnectionToClient>();
            if (!NetworkServer.active || sender == null || !sender.isAuthenticated || !sender.isReady
                || !NetworkServer.connections.TryGetValue(sender.connectionId, out var live) || live.Pointer != sender.Pointer
                || plugin == null || !plugin.Sync.IsCompatible(sender) || channel != 0 || !HeaderValid(reader, Request)) return;
            var id = reader.ReadUInt(); var sequence = reader.ReadUInt();
            if (!Resolve(true, id, out var idol, out var holder, out var bank)
                || holder.PlayerMovement.connectionToClient == null
                || holder.PlayerMovement.connectionToClient.Pointer != sender.Pointer) return;
            var position = holder.transform.position;
            var now = Time.realtimeSinceStartup;
            if (!Finite(position) || !float.IsFinite(now)) return;
            // Bounded by current connections; a reused numeric connection ID gets fresh state.
            _removed.Clear();
            foreach (var pair in _peers)
                if (!NetworkServer.connections.TryGetValue(pair.Key, out var current) || current.Pointer != pair.Value.Connection)
                    _removed.Add(pair.Key);
            foreach (var key in _removed) _peers.Remove(key);
            if (!_peers.TryGetValue(sender.connectionId, out var peer))
                _peers.Add(sender.connectionId, peer = new Peer { Connection = sender.Pointer });
            if (!Newer(sequence, peer.Sequence)) return;
            peer.Sequence = sequence;
            var same = peer.Bank == bank.ConfigId && peer.Idol == id;
            if (now - peer.LastTime < bank.Interval) return;
            if (same && bank.MinDistance > 0)
            {
                var delta = position - peer.Position; delta.y = 0;
                if (delta.sqrMagnitude < bank.MinDistance * bank.MinDistance) return;
            }
            var clip = ChooseClip(_random, bank.Clips.Length, same ? peer.Clip : -1);
            peer.Bank = bank.ConfigId; peer.Idol = id; peer.Clip = clip; peer.Position = position; peer.LastTime = now;
            var eventSequence = Next(ref _eventSequence);
            // Authority, current ownership, sequence and rate checks have all
            // succeeded. Gameplay reactions run once here, never in playback.
            // Consume the request first so a duplicate cannot repeat a reaction.
            bank.OnServerEmission?.Invoke(new HeldIdolContext(idol, holder, true));
            var writer = NetworkWriterPool.Get();
            try
            {
                Header(writer, Playback); writer.WriteUInt(id); writer.WriteUInt(eventSequence);
                writer.WriteUInt(holder.netId); writer.WriteByte((byte)clip);
                writer.WriteFloat(position.x); writer.WriteFloat(position.y); writer.WriteFloat(position.z);
                foreach (var target in NetworkServer.connections.Values)
                    if (target.isAuthenticated && target.isReady && plugin.Sync.IsCompatible(target))
                        target.Send(writer.ToArraySegment(), 0);
            }
            finally { writer.Dispose(); }
            if (plugin.DebugTools.Enabled)
                plugin.Log.LogInfo($"[Audio event] bank={bank.Key}, idol={id}, holder={holder.netId}, clip={clip + 1}, seq={eventSequence}.");
        }
        catch (Exception error) { Fail(error); }
    }
    private void ReceivePlayback(NetworkConnection connection, NetworkReader reader, int channel)
    {
        try
        {
            var server = NetworkClient.connection;
            if (!NetworkClient.isConnected || !NetworkClient.ready || server == null || !server.isAuthenticated
                || connection.Pointer != server.Pointer || CalypsoExpansionSetPlugin.Instance?.Sync.ClientCompatible != true
                || channel != 0 || !HeaderValid(reader, Playback)) return;
            if (_clientConnection != server.Pointer) { _clientConnection = server.Pointer; _receivedSequence = 0; }
            var id = reader.ReadUInt(); var sequence = reader.ReadUInt(); var holderId = reader.ReadUInt();
            var clip = reader.ReadByte();
            var position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            if (!Newer(sequence, _receivedSequence) || !Finite(position)
                || !Resolve(false, id, out _, out var holder, out var bank) || holder.netId != holderId || clip >= bank.Clips.Length)
                return;
            _receivedSequence = sequence;
            var local = EntityManager.Me;
            if (local == null || (local.transform.position - position).sqrMagnitude > 18f * 18f) return;
            // No local prediction: a host hears the same single broadcast as observers.
            var slot = _sourceIndex;
            _sourceIndex = (_sourceIndex + 1) % _channels.Length;
            if (_channels[slot].handle != IntPtr.Zero) _channels[slot].stop();
            Check(RuntimeManager.CoreSystem.playSound(bank.Clips[clip], default, true, out _channels[slot]), "playSound");
            var soundPosition = new VECTOR { x = position.x, y = position.y + .5f, z = position.z };
            var velocity = new VECTOR();
            Check(_channels[slot].set3DAttributes(ref soundPosition, ref velocity), "set3DAttributes");
            Check(_channels[slot].set3DMinMaxDistance(2f, 18f), "set3DMinMaxDistance");
            Check(_channels[slot].setVolume(Volume()), "setVolume");
            Check(_channels[slot].setPaused(false), "unpause");
            if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Audio playback] {bank.Key}/{clip + 1}, FMOD=OK, volume={Volume():F3}.");
        }
        catch (Exception error) { Fail(error); }
    }

    private static float Volume()
    {
        var settings = SoundSettings.I;
        if (settings == null) return .65f;
        var master = Mathf.InverseLerp(settings.generalVolumeRange.x, settings.generalVolumeRange.y, settings.generalVolume);
        var sfx = Mathf.InverseLerp(settings.sfxVolumeRange.x, settings.sfxVolumeRange.y, settings.sfxVolume);
        return .65f * master * sfx;
    }
    internal static void UpdateListener(SetFovShakeData source)
    {
        var service = _instance;
        if (service == null || service._disposed) return;
        try
        {
            var local = EntityManager.Me;
            if (!NetworkClient.isConnected || !NetworkClient.ready || local == null) { service.StopAudio(); return; }
            if (!source._ready || source._playerPerlinShake != local.playerPerlinShake) return;
            // The game's existing FMOD listener supplies position/orientation.
            // Only owned channels are updated; no extra global listener or mixer.
            var volume = Volume();
            for (var i = 0; i < service._channels.Length; i++)
            {
                if (service._channels[i].handle == IntPtr.Zero) continue;
                var result = service._channels[i].isPlaying(out var playing);
                if (result != RESULT.OK || !playing) service._channels[i] = default;
                else service._channels[i].setVolume(volume);
            }
        }
        catch (Exception error) { service.Fail(error); }
    }

    private void EnsureHandlers()
    {
        Install(NetworkServer.handlers, _serverHandler);
        Install(NetworkClient.handlers, _clientHandler);
    }
    private static void Install(Il2CppSystem.Collections.Generic.Dictionary<ushort, NetworkMessageDelegate> handlers, NetworkMessageDelegate handler)
    {
        if (handlers.TryGetValue(MessageId, out var existing))
        {
            if (existing.Pointer != handler.Pointer) throw new InvalidOperationException("Held audio message ID conflicts with another MOD.");
        }
        else handlers.Add(MessageId, handler);
    }
    internal static void ResetTransport(bool server)
    {
        var service = _instance;
        if (service == null || service._disposed) return;
        try
        {
            if (server) { service._peers.Clear(); service._eventSequence = 0; }
            else { service._clientConnection = IntPtr.Zero; service._receivedSequence = service._requestSequence = 0; service.StopAudio(); }
            service.EnsureHandlers();
        }
        catch (Exception error) { service.Fail(error); }
    }
    private void StopAudio() { for (var i = 0; i < _channels.Length; i++) { if (_channels[i].handle != IntPtr.Zero) _channels[i].stop(); _channels[i] = default; } }
    private void Fail(Exception error)
    {
        if (_disposed) return;
        Dispose();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Held audio disabled after an error: {error}");
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (NetworkServer.handlers.TryGetValue(MessageId, out var s) && s.Pointer == _serverHandler.Pointer) NetworkServer.handlers.Remove(MessageId);
        if (NetworkClient.handlers.TryGetValue(MessageId, out var c) && c.Pointer == _clientHandler.Pointer) NetworkClient.handlers.Remove(MessageId);
        StopAudio();
        _peers.Clear();
        if (_instance == this) _instance = null;
    }
}

[HarmonyPatch(typeof(NetworkServer), "RegisterMessageHandlers")]
internal static class HeldAudioServerHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => HeldIdolAudio.ResetTransport(true);
}
[HarmonyPatch(typeof(NetworkClient), "RegisterMessageHandlers")]
internal static class HeldAudioClientHandlersPatch
{
    [HarmonyPostfix] private static void Postfix() => HeldIdolAudio.ResetTransport(false);
}
[HarmonyPatch(typeof(SetFovShakeData), "LateUpdate")]
internal static class HeldAudioListenerPatch
{
    [HarmonyPostfix] private static void Postfix(SetFovShakeData __instance) => HeldIdolAudio.UpdateListener(__instance);
}
