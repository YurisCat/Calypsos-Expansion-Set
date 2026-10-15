using Entities.Monster;
using Entities.Player;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Visuals;

internal sealed class MonsterPlayerAppearance : IPlayerAppearance
{
    private static readonly HashSet<MonsterPlayerAppearance> Instances = new();
    private readonly PlayerEntity _player;
    private readonly GameObject _playerRig;
    private readonly MonsterEntity _template;
    private readonly VisualRigCopy _copy;
    private readonly Renderer[] _hidden;
    private readonly bool[] _previousOff;
    private readonly Items.Item? _heldItem;
    private readonly FlashlightTool? _flashlight;
    private readonly FlashlightController? _flashlightController;
    private readonly Light? _light;
    private readonly TextMeshProUGUI? _nickname;
    private readonly VisualEffect? _coldMouth;
    private readonly AnimationClip? _attack;
    private readonly Func<float?>? _attackElapsed;
    private bool _previousColdMouthEnabled, _coldMouthSuppressed;
    private bool _previousNicknameEnabled, _nicknameSuppressed;
    private bool _previousLightEnabled, _lightSuppressed;
    private readonly Vector3 _scale;
    private readonly int _layer;
    private GameObject? _animationPivot;
    private AnimationClip? _idle, _walk, _currentClip;
    private Transform[] _animatedBones = Array.Empty<Transform>();
    private Vector3[] _blendPositions = Array.Empty<Vector3>(), _blendScales = Array.Empty<Vector3>();
    private Quaternion[] _blendRotations = Array.Empty<Quaternion>();
    private float _clipTime, _blendTime;
    private bool _disposed;
    private Vector3 _previousPosition;
    private float _lastTime;
    private int _animationFrame = -1;

    internal static IPlayerAppearance? Create(PlayerEntity player, MonsterEntity template,
        AnimationClip? attack = null, Func<float?>? attackElapsed = null)
    {
        if (player == EntityManager.Me && !player.UseFreeCamWorldRig) return null;
        var rig = player.PlayerWorldRigController;
        if (rig == null || player.WorldRigInstance == null || rig.SkinnedMeshRenderer == null) return null;
        return new MonsterPlayerAppearance(player, player.WorldRigInstance, rig.SkinnedMeshRenderer.gameObject.layer, template, attack, attackElapsed);
    }

    private MonsterPlayerAppearance(PlayerEntity player, GameObject playerRig, int layer, MonsterEntity template,
        AnimationClip? attack, Func<float?>? attackElapsed)
    {
        _player = player; _playerRig = playerRig; _template = template; _layer = layer;
        _attack = attack; _attackElapsed = attackElapsed;
        _heldItem = player.InventoryNew?.InHand;
        _flashlight = player.FlashLight;
        _flashlightController = _flashlight?.FlashLightController;
        _light = _flashlightController?.SelfLight;
        _nickname = player.NickWorldSpaceUI?.nickText;
        _coldMouth = player.PlayerWorldRigController?.ColdMouthVfx;
        _hidden = playerRig.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.GetComponentInParent<Items.Item>() == null)
            .Concat(_heldItem != null ? _heldItem.GetComponentsInChildren<Renderer>(true).ToArray() : Array.Empty<Renderer>())
            .Concat(_flashlight != null ? _flashlight.GetComponentsInChildren<Renderer>(true).ToArray() : Array.Empty<Renderer>())
            .Concat(_flashlightController != null ? _flashlightController.GetComponentsInChildren<Renderer>(true).ToArray() : Array.Empty<Renderer>())
            .DistinctBy(renderer => renderer.Pointer).ToArray();
        _previousOff = _hidden.Select(renderer => renderer.forceRenderingOff).ToArray();
        _copy = new VisualRigCopy(template.transform, Vector3.one, hideSource: false);
        try
        {
            // Preserve the native monster's visual size, independently of the
            // player's scale/miniature status. Physics stays on the native player.
            _scale = template.transform.lossyScale;
            SetupAnimation();
            _previousPosition = player.transform.position;
            _lastTime = Time.time;
            Instances.Add(this);
        }
        catch { Dispose(); throw; }
    }

    private void SetupAnimation()
    {
        var animator = _template.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return;
        var source = animator.runtimeAnimatorController.animationClips;
        AnimationClip? idle = null, walk = null;
        foreach (var clip in source)
        {
            if (clip == null) continue;
            var name = clip.name.ToLowerInvariant();
            if (idle == null && (name.Contains("idle") || name.Contains("breath"))) idle = clip;
            if (walk == null && (name.Contains("walk") || name.Contains("locomotion") || name.Contains("run"))) walk = clip;
        }
        if (idle == null && walk == null && _attack == null) return;
        var pivot = _copy.CopiedTransform(animator.transform);
        if (pivot == null) return;
        _animationPivot = pivot.gameObject;
        // Runtime sampling of non-Legacy clips requires an Animator. Borrow
        // only its avatar: no controller, state behaviours, events or root motion.
        var sampler = _animationPivot.AddComponent<Animator>();
        sampler.avatar = animator.avatar;
        sampler.applyRootMotion = false;
        sampler.fireEvents = false;
        sampler.enabled = false;
        _idle = idle ?? walk;
        _walk = walk ?? idle;
        _animatedBones = pivot.GetComponentsInChildren<Transform>(true).ToArray();
        _blendPositions = new Vector3[_animatedBones.Length];
        _blendScales = new Vector3[_animatedBones.Length];
        _blendRotations = new Quaternion[_animatedBones.Length];
    }

    public bool Matches(PlayerEntity player) => !_disposed && _copy.Alive && player == _player
        && (player != EntityManager.Me || player.UseFreeCamWorldRig)
        && player.PlayerWorldRigController != null && player.WorldRigInstance == _playerRig
        && player.InventoryNew?.InHand == _heldItem && player.FlashLight == _flashlight
        && _flashlight?.FlashLightController == _flashlightController && _flashlightController?.SelfLight == _light
        && player.NickWorldSpaceUI?.nickText == _nickname
        && player.PlayerWorldRigController?.ColdMouthVfx == _coldMouth;

    public void Render()
    {
        if (_disposed || _player == null || _playerRig == null) return;
        // The view can change after Tick. Restore before drawing the owner's POV.
        if (_player == EntityManager.Me && !_player.UseFreeCamWorldRig) { Dispose(); return; }
        // World-mouth VFX is controlled independently by native biome updates.
        // Suppress that instance explicitly; the owner's camera VFX stays native.
        if (_player != EntityManager.Me && _coldMouth != null)
        {
            if (!_coldMouthSuppressed) _previousColdMouthEnabled = _coldMouth.enabled;
            if (_coldMouth.enabled) _coldMouth.enabled = false;
            _coldMouthSuppressed = true;
        }
        var visible = _playerRig.activeInHierarchy;
        for (var index = 0; index < _hidden.Length; index++)
            if (_hidden[index] != null) _hidden[index].forceRenderingOff = true;
        // Native nickname fades keep updating alpha and visibility. Hide only
        // this text component, without changing the setting, nickname or tween.
        if (_nickname != null)
        {
            if (!_nicknameSuppressed) _previousNicknameEnabled = _nickname.enabled;
            _nickname.enabled = false;
            _nicknameSuppressed = true;
        }
        // Restore at the render boundary, so native switch/flicker/sense updates
        // between frames retain their own state.
        if (_light != null && !_lightSuppressed)
        {
            _previousLightEnabled = _light.enabled;
            _light.enabled = false;
            _lightSuppressed = true;
        }
        var position = _player.transform.position;
        var rotation = _playerRig.transform.rotation * _template.transform.rotation;
        _copy.Root.gameObject.SetActive(visible);
        // Multiple render contexts can share a frame. Reuse its pose so zero
        // elapsed time cannot switch walking back to idle and restart the clip.
        if (_animationFrame == Time.frameCount)
        {
            _copy.ShowAt(position, rotation, _scale, visible, _layer);
            return;
        }
        _animationFrame = Time.frameCount;
        var elapsed = Time.time - _lastTime;
        var delta = position - _previousPosition; delta.y = 0;
        var moving = elapsed > 0.0001f && delta.sqrMagnitude / (elapsed * elapsed) > 0.08f;
        _previousPosition = position; _lastTime = Time.time;
        var attackElapsed = _attack != null ? _attackElapsed?.Invoke() : null;
        var attacking = attackElapsed is >= 0 && attackElapsed < _attack!.length;
        var next = attacking ? _attack : moving ? _walk : _idle;
        if (next == null && _currentClip != null)
        {
            _copy.SyncPose();
            _currentClip = null;
        }
        if (visible && _animationPivot != null && next != null)
        {
            if (_currentClip != next)
            {
                for (var index = 0; index < _animatedBones.Length; index++)
                {
                    _blendPositions[index] = _animatedBones[index].localPosition;
                    _blendRotations[index] = _animatedBones[index].localRotation;
                    _blendScales[index] = _animatedBones[index].localScale;
                }
                _currentClip = next;
                _clipTime = _blendTime = 0;
            }
            _clipTime += Math.Max(0, elapsed);
            _blendTime += Math.Max(0, elapsed);
            // Sample explicitly; the controller-free Animator never ticks.
            var sampleTime = attacking ? attackElapsed!.Value : Mathf.Repeat(_clipTime, Math.Max(0.01f, next.length));
            next.SampleAnimation(_animationPivot, sampleTime);
            if (_blendTime < 0.15f)
            {
                var blend = _blendTime / 0.15f;
                for (var index = 0; index < _animatedBones.Length; index++)
                {
                    var bone = _animatedBones[index];
                    bone.localPosition = Vector3.Lerp(_blendPositions[index], bone.localPosition, blend);
                    bone.localRotation = Quaternion.Slerp(_blendRotations[index], bone.localRotation, blend);
                    bone.localScale = Vector3.Lerp(_blendScales[index], bone.localScale, blend);
                }
            }
        }
        // Root curves in a sampled clip must not move or turn the attachment.
        _copy.ShowAt(position, rotation, _scale, visible, _layer);
    }

    private void RestoreLight()
    {
        if (!_lightSuppressed) return;
        if (_light != null && !_light.enabled) _light.enabled = _previousLightEnabled;
        _lightSuppressed = false;
    }

    internal static bool TryGetBounds(PlayerEntity player, out Bounds bounds)
    {
        foreach (var instance in Instances)
            if (instance._player == player && !instance._disposed && instance._copy.TryGetBounds(out bounds)) return true;
        bounds = default;
        return false;
    }

    internal static GameObject? HidePreviewBody(PlayerEntity player)
    {
        if (player != EntityManager.Me) return null;
        foreach (var instance in Instances)
        {
            if (instance._player != player || instance._disposed) continue;
            var body = instance._copy.Root.gameObject;
            if (!body.activeSelf) return null;
            body.SetActive(false);
            return body;
        }
        return null;
    }

    internal static void AfterRendering()
    {
        foreach (var instance in Instances) instance.RestoreLight();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Instances.Remove(this);
        RestoreLight();
        if (_nicknameSuppressed && _nickname != null && !_nickname.enabled)
            _nickname.enabled = _previousNicknameEnabled;
        _nicknameSuppressed = false;
        _copy.Dispose();
        for (var index = 0; index < _hidden.Length; index++)
            if (_hidden[index] != null && _hidden[index].forceRenderingOff) _hidden[index].forceRenderingOff = _previousOff[index];
        _idle = _walk = _currentClip = null;
        _animationPivot = null;
        if (_coldMouthSuppressed && _coldMouth != null && !_coldMouth.enabled)
        {
            _coldMouth.enabled = _previousColdMouthEnabled;
            // Re-enabling a VFX can restart its default event. Re-evaluate the
            // current biome/health, rather than replaying the state at pickup.
            if (_previousColdMouthEnabled && _player != null && _player != EntityManager.Me
                && _player.PlayerWorldRigController?.ColdMouthVfx == _coldMouth)
                _player.CliRefreshWorldVfx();
        }
        _coldMouthSuppressed = false;
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "EndContextRendering")]
internal static class MonsterDisguiseLightRestorePatch
{
    [HarmonyPostfix] private static void Postfix() => MonsterPlayerAppearance.AfterRendering();
}
