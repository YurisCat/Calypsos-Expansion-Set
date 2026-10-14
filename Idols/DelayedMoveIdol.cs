using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using UnityEngine;
using User;

namespace CalypsoExpansionSet.Idols;

internal sealed class DelayedMoveIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000007UL;
    internal const float DelaySeconds = 0.3f;
    internal static IdolDefinition Definition => new(
        "delayed_move", "迟钝雕像", ConfigId, 0x4D490007U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new DelayedMoveIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "起步慢半拍，停步也延后",
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "todo_delayed_move"),
        ContentVersion = 2
    };

    private static DelayedMoveIdol? _active;
    private HeldIdolContext _context;
    private readonly MovementDelayBuffer _buffer = new(DelaySeconds);
    private Vector2 _lastLogged, _lastRaw;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (LocalHeldGuard.Ready(context, ConfigId)) _active = this;
        else { _buffer.Clear(); if (_active == this) _active = null; }
    }

    internal static void Filter(InputsManager source, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null) return;
        if (source != InputsManager.Instance || !LocalHeldGuard.Ready(effect._context, ConfigId))
        {
            effect._buffer.Clear();
            if (_active == effect) _active = null;
            return;
        }
        var raw = value;
        value = effect._buffer.Read(raw, Time.realtimeSinceStartup);
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin?.DebugTools.Enabled == true && raw != effect._lastRaw)
            plugin.Log.LogInfo($"[Delayed move input] raw=({raw.x:F2},{raw.y:F2}), time={Time.realtimeSinceStartup:F3}s.");
        effect._lastRaw = raw;
        if (plugin?.DebugTools.Enabled == true && value != effect._lastLogged)
        {
            effect._lastLogged = value;
            plugin.Log.LogInfo($"[Delayed move] raw=({raw.x:F2},{raw.y:F2}), "
                + $"output=({value.x:F2},{value.y:F2}), delay={DelaySeconds:F2}s, time={Time.realtimeSinceStartup:F3}s.");
        }
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        _buffer.Clear(); _lastLogged = _lastRaw = default; _context = default;
    }
}

// Movement samples, including release (zero), are delayed; no actions are replayed.
// Bounded storage and no per-sample allocations. A long frame returns the latest due value.
internal sealed class MovementDelayBuffer
{
    private readonly record struct Sample(float Due, Vector2 Value);
    private readonly Sample[] _samples = new Sample[512];
    private readonly float _delay;
    private int _head, _count;
    private Vector2 _raw, _output;
    private float _lastTime = float.NegativeInfinity;
    internal MovementDelayBuffer(float delay) => _delay = delay;
    internal Vector2 Read(Vector2 raw, float now)
    {
        if (!float.IsFinite(now) || !float.IsFinite(raw.x) || !float.IsFinite(raw.y)) { Clear(); return Vector2.zero; }
        if (now < _lastTime) Clear();
        _lastTime = now;
        while (_count > 0 && _samples[_head].Due <= now)
        {
            _output = _samples[_head].Value; _head = (_head + 1) % _samples.Length; _count--;
        }
        if (raw != _raw)
        {
            // An extreme polling burst cannot grow an unbounded input backlog.
            if (_count == _samples.Length) { _head = (_head + 1) % _samples.Length; _count--; }
            _samples[(_head + _count) % _samples.Length] = new(now + _delay, raw);
            _count++; _raw = raw;
        }
        return _output;
    }
    internal void Clear() { _head = _count = 0; _raw = _output = Vector2.zero; _lastTime = float.NegativeInfinity; }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class DelayedMoveInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => DelayedMoveIdol.Filter(__instance, ref __result);
}
