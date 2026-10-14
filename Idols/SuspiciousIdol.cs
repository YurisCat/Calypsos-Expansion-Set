using CalypsoExpansionSet.Visuals;
using CalypsoExpansionSet.Audio;
using FMODUnity;
using Mirror;
using UnityEngine;
using _200_Dev.Sound;

namespace CalypsoExpansionSet.Idols;

internal sealed class SuspiciousIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D4900000000000AUL;
    internal const float MinimumInterval = 5f, MaximumInterval = 9f;
    internal const float NoiseRange = 18f;
    internal static IdolDefinition Definition => new(
        "suspicious", "疑神雕像", ConfigId, 0x4D49000AU, 7331884207844971565UL,
        IdolEffectAuthority.ServerGameplay, () => new SuspiciousIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "怪声四起，干吓自己",
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "todo_suspicious"),
        CreateRuntime = () => new MonsterCatalogRuntime(),
        ContentVersion = 3
    };
    private readonly System.Random _random = new();
    private readonly List<EventReference> _sounds = new();
    private readonly HashSet<string> _guids = new(StringComparer.Ordinal);
    private float _nextSound;

    public override void Start(HeldIdolContext context)
    {
        _sounds.Clear(); _guids.Clear();
        foreach (var monster in NativeMonsterCatalog.Current.AllPrefabs())
        {
            AddSound(monster.hurtSound);
            AddSound(monster.deathSound);
        }
        if (_sounds.Count == 0) throw new InvalidOperationException("No native one-shot monster sounds are available.");
        _nextSound = Time.time + Interval();
    }

    private void AddSound(EventReference sound)
    {
        if (sound.IsNull) return;
        var description = RuntimeManager.GetEventDescription(sound);
        if (description.isValid() && description.isOneshot(out var oneShot) == FMOD.RESULT.OK && oneShot
            && description.is3D(out var spatial) == FMOD.RESULT.OK && spatial
            && _guids.Add(sound.Guid.ToString())) _sounds.Add(sound);
    }
    private float Interval() => MinimumInterval + (float)_random.NextDouble() * (MaximumInterval - MinimumInterval);

    public override void Tick(HeldIdolContext context)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (!NetworkServer.active || !context.GameplayReady || plugin == null || !plugin.Sync.AllReadyPeersCompatible
            || !HeldAppearanceRuntime.Held(context.Holder, ConfigId, out var held) || held != context.Idol) return;
        if (Time.time < _nextSound) return;
        _nextSound = Time.time + Interval();
        var position = context.Holder.transform.position;
        var audio = NetworkAudioManager.Instance;
        if (audio == null || audio.netId == 0 || HearingSenseManager.Instance == null)
            throw new InvalidOperationException("Native network audio/hearing manager is unavailable.");
        var sound = _sounds[_random.Next(_sounds.Count)];
        // Native TargetRPC serializes the same event and position for each peer.
        // It is an event, not a snapshot; late joiners never replay old sounds.
        foreach (var peer in NetworkServer.connections.Values)
            if (peer != null && peer.isAuthenticated && peer.isReady && peer.identity != null && plugin.Sync.IsCompatible(peer))
                audio.TargetPlayOneShot(peer, sound, position);

        // Native player-hearing goals resolve the source player's position.
        // Keep playback and hearing at that same source instead of a false origin.
        IdolHearing.Emit(context, ConfigId, NoiseRange, "Suspicious");
        if (plugin.DebugTools.Enabled)
            plugin.Log.LogInfo($"[Suspicious] idol={context.Idol.netId}, sound={sound.Guid}, position={position}, noiseRange={NoiseRange}.");
    }

    public override void Dispose() { _sounds.Clear(); _guids.Clear(); _nextSound = float.PositiveInfinity; }
}
