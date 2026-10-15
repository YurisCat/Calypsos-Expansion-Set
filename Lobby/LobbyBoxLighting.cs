using EnvironmentElements;
using HarmonyLib;
using Lights;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

// Purely local fill lighting. Sample a native room fixture after its animation
// updates, including current state on late creation. Never write native fixtures.
internal sealed class LobbyBoxLighting : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Transform _model;
    private readonly Room _room;
    private readonly Vector3 _target;
    private readonly List<(Light Light, float Intensity, float Range)> _lights = new();
    private GameObject? _root;
    private ControllableLight? _referenceController;
    private Light? _reference;
    private float _referenceBase, _nextSearch;
    private Vector3 _lastSearchPosition;
    private bool _faulted;
    internal int Preset { get; private set; } = -1;
    internal float Brightness { get; private set; } = 1f;
    internal bool HasReference => _reference != null && _referenceController != null;

    internal LobbyBoxLighting(CalypsoExpansionSetPlugin plugin, Transform model, Room room, Bounds bounds)
    { _plugin = plugin; _model = model; _room = room; _target = bounds.center; }

    internal static string Name(int preset) => preset switch
    {
        0 => LobbyBoxText.Pick("关闭补光", "Fill light off"),
        1 => LobbyBoxText.Pick("左前柔光 / 聚光", "Soft front-left / spot"),
        2 => LobbyBoxText.Pick("右前柔光 / 聚光", "Soft front-right / spot"),
        3 => LobbyBoxText.Pick("正面漫射 / 点光", "Front fill / point"),
        4 => LobbyBoxText.Pick("双侧柔光 / 双聚光", "Soft cross-light / two spots"),
        5 => LobbyBoxText.Pick("顶部与侧面 / 混合光", "Top and side / mixed"),
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    internal void SetBrightness(float value)
    {
        if (!float.IsFinite(value) || value is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(value));
        if (Brightness == value) return;
        Brightness = value;
        Sync();
    }

    internal void SetPreset(int preset)
    {
        if (preset is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(preset));
        if (Preset == preset) return;
        ClearLights();
        Preset = preset;
        if (preset == 0 || _faulted) return;
        _root = new GameObject("Calypso Box Fill Lights") { hideFlags = HideFlags.DontSave };
        _root.transform.SetParent(_model, false);
        var warm = new Color(1f, 0.91f, 0.80f);
        var neutral = new Color(1f, 0.97f, 0.93f);
        switch (preset)
        {
            case 1: Add(LightType.Spot, new Vector3(-0.32f, 0.55f, -0.24f), warm, 1.4f, 1.4f); break;
            case 2: Add(LightType.Spot, new Vector3(0.32f, 0.55f, -0.24f), neutral, 1.4f, 1.4f); break;
            case 3: Add(LightType.Point, new Vector3(0, 0.42f, -0.08f), warm, 0.85f, 1.05f); break;
            case 4:
                Add(LightType.Spot, new Vector3(-0.32f, 0.43f, 0.22f), warm, 0.9f, 1.25f);
                Add(LightType.Spot, new Vector3(0.32f, 0.43f, -0.22f), neutral, 0.9f, 1.25f); break;
            case 5:
                Add(LightType.Spot, new Vector3(0, 0.43f, 0.45f), neutral, 1.1f, 1.4f);
                Add(LightType.Point, new Vector3(-0.28f, 0.25f, -0.12f), warm, 0.35f, 0.8f); break;
        }
        Sync();
    }

    private void Add(LightType type, Vector3 position, Color color, float intensity, float range)
    {
        var obj = new GameObject("Box fill " + type) { hideFlags = HideFlags.DontSave };
        obj.transform.SetParent(_root!.transform, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.LookRotation(_target - position);
        var light = obj.AddComponent<Light>();
        light.enabled = false; light.type = type; light.color = color; light.intensity = 0;
        light.range = range; light.spotAngle = 105; light.innerSpotAngle = 45;
        light.shadows = LightShadows.Soft; light.shadowStrength = 0.65f;
        light.shadowBias = 0.02f; light.shadowNormalBias = 0.025f;
        light.renderMode = LightRenderMode.ForcePixel;
        _lights.Add((light, intensity, range));
    }

    internal void Maintenance()
    {
        if (_faulted || _model == null || _room == null) return;
        try
        {
            if (Time.realtimeSinceStartup < _nextSearch) return;
            _nextSearch = Time.realtimeSinceStartup + 1f;
            if (HasReference && Vector3.SqrMagnitude(_lastSearchPosition - _model.position) < 0.25f) return;
            _lastSearchPosition = _model.position;
            _reference = null; _referenceController = null; _referenceBase = 0;
            var scope = _room.prefabRoom != null ? _room.prefabRoom : _room.gameObject;
            var best = float.PositiveInfinity;
            foreach (var controller in scope.GetComponentsInChildren<ControllableLight>(true))
            {
                if (controller == null || controller.lights == null || controller._savedIntensities == null) continue;
                for (var i = 0; i < controller.lights.Count && i < controller._savedIntensities.Count; i++)
                {
                    var light = controller.lights[i];
                    var baseline = controller._savedIntensities[i];
                    if (light == null || baseline <= 0.0001f || !float.IsFinite(baseline)) continue;
                    var distance = (light.transform.position - _model.position).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance; _reference = light; _referenceController = controller; _referenceBase = baseline;
                }
            }
            if (HasReference && _plugin.DebugTools.Enabled)
                _plugin.Log.LogInfo($"[Lobby lighting] following native fixture {_reference!.name}; baseline={_referenceBase}.");
        }
        catch (Exception error) { Fault(error); }
    }

    internal void Sync()
    {
        if (_faulted || _model == null) return;
        try
        {
            var envelope = LobbyBoxTuning.LightEnvelope(HasReference,
                HasReference && _reference!.enabled && _reference.gameObject.activeInHierarchy
                    && _referenceController!.isActiveAndEnabled,
                _reference != null ? _reference.intensity : 0, _referenceBase);
            var scale = _model.lossyScale;
            var multiplier = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            foreach (var entry in _lights)
            {
                if (entry.Light == null) continue;
                entry.Light.enabled = envelope > 0 && Brightness > 0;
                entry.Light.intensity = LobbyBoxTuning.FillIntensity(entry.Intensity, envelope, multiplier, Brightness);
                entry.Light.range = entry.Range * multiplier;
            }
        }
        catch (Exception error) { Fault(error); }
    }

    private void Fault(Exception error)
    {
        _faulted = true; ClearLights();
        _plugin.Log.LogError("[Lobby lighting] fill lights disabled: " + error);
    }
    private void ClearLights()
    {
        foreach (var entry in _lights) if (entry.Light != null) entry.Light.enabled = false;
        _lights.Clear();
        if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
        _root = null;
    }
    public void Dispose() { ClearLights(); _reference = null; _referenceController = null; }
}

[HarmonyPatch(typeof(LightManager), "LateUpdate")]
internal static class LobbyBoxLightingUpdatePatch
{
    private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.Lighting?.Sync();
}
