using Entities.Player;
using Settings;
using UnityEngine;
using UnityEngine.VFX;
using User;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Curses;

// Reuses only the native curse VFX asset. No CurseSoul, navigation, RPC or
// gameplay callback is created; the existing curse/board flow never waits here.
internal sealed class DisguiseArrivalEffects
{
    private const float PendingLifetime = 1.5f, ArrivalDuration = 1f, ParticleTail = 2f;
    private readonly DisguiseCurse _curse;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly List<DisguiseEntry> _pending = new();
    private readonly List<Arrival> _effects = new();
    private float _pendingUntil;

    internal DisguiseArrivalEffects(DisguiseCurse curse, CalypsoExpansionSetPlugin plugin)
    { _curse = curse; _plugin = plugin; }

    internal void Queue(IReadOnlyList<DisguiseEntry> entries)
    {
        Clear();
        _pending.AddRange(entries);
        _pendingUntil = Time.realtimeSinceStartup + PendingLifetime;
    }

    internal void Tick(bool active)
    {
        if (!active) { Clear(); return; }
        try
        {
            for (var index = _effects.Count - 1; index >= 0; index--)
                if (!_effects[index].Tick(_curse))
                { _effects[index].Dispose(); _effects.RemoveAt(index); }
            if (_pending.Count == 0) return;
            if (Time.realtimeSinceStartup > _pendingUntil) { _pending.Clear(); return; }
            var prefab = VFXSettings.I?.cursePrefab;
            var source = prefab != null ? prefab.visualEffect : null;
            if (source == null || source.visualEffectAsset == null) return;
            for (var index = _pending.Count - 1; index >= 0; index--)
            {
                var entry = _pending[index];
                var player = UserInstancesManager.Instance?.Get(entry.User)?.playerEntity;
                if (player == null || player.netId != entry.Player || !_curse.TryLook(player, out _)) continue;
                // A dead player has no transformation to announce. Revival must
                // not turn an already-consumed event into a fresh effect.
                if (player.CurrentHealthSync <= 0) { _pending.RemoveAt(index); continue; }
                var body = player.PlayerWorldRigController?.SkinnedMeshRenderer;
                if (body == null || !player.gameObject.activeInHierarchy) continue;
                _effects.Add(new Arrival(player, source, prefab!.soulLight, body.bounds.center));
                _pending.RemoveAt(index);
            }
        }
        catch (Exception error)
        {
            Clear();
            _plugin.Log.LogWarning($"Disguise arrival visuals skipped; disguise remains active: {error}");
        }
    }

    internal void Clear()
    {
        _pending.Clear();
        foreach (var effect in _effects) effect.Dispose();
        _effects.Clear();
    }

    private sealed class Arrival : IDisposable
    {
        private readonly PlayerEntity _player;
        private readonly float _startedAt, _intensity;
        private readonly Vector3 _scale;
        private GameObject? _root;
        private VisualEffect? _vfx;
        private Light? _light;
        private bool _stopped;

        internal Arrival(PlayerEntity player, VisualEffect source, Light? sourceLight, Vector3 position)
        {
            _player = player;
            _startedAt = Time.realtimeSinceStartup;
            _scale = source.transform.localScale;
            _intensity = sourceLight != null ? sourceLight.intensity : 0f;
            try
            {
                _root = new GameObject("Calypso disguise arrival") { hideFlags = HideFlags.DontSave };
                _root.SetActive(false);
                _root.layer = source.gameObject.layer;
                _root.transform.SetPositionAndRotation(position, Quaternion.identity);
                _root.transform.localScale = _scale;
                _vfx = _root.AddComponent<VisualEffect>();
                // The native curse prefab has no VFX property overrides. Share
                // its graph asset without cloning any prefab behaviours.
                _vfx.visualEffectAsset = source.visualEffectAsset;
                _vfx.initialEventID = source.initialEventID;
                if (sourceLight != null)
                {
                    _light = _root.AddComponent<Light>();
                    _light.type = sourceLight.type;
                    _light.color = sourceLight.color;
                    _light.range = sourceLight.range;
                    _light.intensity = _intensity;
                    _light.shadows = LightShadows.None;
                    _light.cullingMask = sourceLight.cullingMask;
                }
                _root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }

        internal bool Tick(DisguiseCurse curse)
        {
            if (_root == null || _player == null || !_player.gameObject.activeInHierarchy
                || _player.CurrentHealthSync <= 0 || !curse.TryLook(_player, out _)) return false;
            var elapsed = Time.realtimeSinceStartup - _startedAt;
            if (elapsed >= ArrivalDuration + ParticleTail) return false;
            var body = _player.PlayerWorldRigController?.SkinnedMeshRenderer;
            if (body == null) return false;
            _root.transform.position = body.bounds.center;
            var progress = Mathf.Clamp01(elapsed / ArrivalDuration);
            // Native Arrived scales to half over one second (InQuad), fades
            // its light (InOutSine), then stops emission and lets particles die.
            _root.transform.localScale = _scale * (1f - 0.5f * progress * progress);
            if (_light != null) _light.intensity = _intensity * (1f + Mathf.Cos(Mathf.PI * progress)) * 0.5f;
            if (!_stopped && progress >= 1f)
            {
                _stopped = true;
                _vfx?.Stop();
                if (_light != null) _light.enabled = false;
            }
            return true;
        }

        public void Dispose()
        {
            if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
            _root = null; _vfx = null; _light = null;
        }
    }
}
