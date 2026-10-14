using Entities.Monster;
using Entities.Player;
using UnityEngine;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Visuals;

internal sealed class MonsterPlayerAppearance : IPlayerAppearance
{
    private readonly PlayerEntity _player;
    private readonly GameObject _playerRig;
    private readonly MonsterEntity _template;
    private readonly VisualRigCopy _copy;
    private readonly Renderer[] _hidden;
    private readonly bool[] _previousOff;
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

    internal static IPlayerAppearance? Create(PlayerEntity player, MonsterEntity template)
    {
        if (player == EntityManager.Me && !player.UseFreeCamWorldRig) return null;
        var rig = player.PlayerWorldRigController;
        if (rig == null || player.WorldRigInstance == null || rig.SkinnedMeshRenderer == null) return null;
        return new MonsterPlayerAppearance(player, player.WorldRigInstance, rig.SkinnedMeshRenderer.gameObject.layer, template);
    }

    private MonsterPlayerAppearance(PlayerEntity player, GameObject playerRig, int layer, MonsterEntity template)
    {
        _player = player; _playerRig = playerRig; _template = template; _layer = layer;
        _hidden = playerRig.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.GetComponentInParent<Items.Item>() == null).ToArray();
        _previousOff = _hidden.Select(renderer => renderer.forceRenderingOff).ToArray();
        _copy = new VisualRigCopy(template.transform, Vector3.one, hideSource: false);
        try
        {
            // Match approximately the player's standing height while preserving
            // the monster's proportions. Native colliders/head remain untouched.
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var skin in template.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bounds = skin.localBounds;
                var bottom = skin.transform.TransformPoint(bounds.min).y;
                var top = skin.transform.TransformPoint(bounds.max).y;
                min = Math.Min(min, Math.Min(bottom, top));
                max = Math.Max(max, Math.Max(bottom, top));
            }
            var height = max - min;
            var multiplier = float.IsFinite(height) && height > 0.05f ? Math.Clamp(1.8f / height, 0.05f, 8f) : 1f;
            _scale = template.transform.lossyScale * multiplier;
            SetupAnimation();
            _previousPosition = player.transform.position;
            _lastTime = Time.time;
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
        if (idle == null && walk == null) return;
        var pivot = _copy.CopiedTransform(animator.transform);
        if (pivot == null) return;
        _animationPivot = pivot.gameObject;
        _idle = idle ?? walk;
        _walk = walk ?? idle;
        _animatedBones = pivot.GetComponentsInChildren<Transform>(true).ToArray();
        _blendPositions = new Vector3[_animatedBones.Length];
        _blendScales = new Vector3[_animatedBones.Length];
        _blendRotations = new Quaternion[_animatedBones.Length];
    }

    public bool Matches(PlayerEntity player) => !_disposed && _copy.Alive && player == _player
        && (player != EntityManager.Me || player.UseFreeCamWorldRig)
        && player.PlayerWorldRigController != null && player.WorldRigInstance == _playerRig;

    public void Render()
    {
        if (_disposed || _player == null || _playerRig == null) return;
        var visible = _playerRig.activeInHierarchy && (_player != EntityManager.Me || _player.UseFreeCamWorldRig);
        for (var index = 0; index < _hidden.Length; index++)
            if (_hidden[index] != null) _hidden[index].forceRenderingOff = true;
        var position = _player.transform.position;
        var elapsed = Time.time - _lastTime;
        var delta = position - _previousPosition; delta.y = 0;
        var moving = elapsed > 0.0001f && delta.sqrMagnitude / (elapsed * elapsed) > 0.08f;
        _previousPosition = position; _lastTime = Time.time;
        var next = moving ? _walk : _idle;
        if (_animationPivot != null && next != null)
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
            // Sample the borrowed clip onto the visual-only hierarchy. There is
            // no Animator/Animation component, event receiver, AI or root motion.
            next.SampleAnimation(_animationPivot, Mathf.Repeat(_clipTime, Math.Max(0.01f, next.length)));
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
        _copy.ShowAt(position, _player.transform.rotation * _template.transform.rotation, _scale, visible, _layer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _copy.Dispose();
        for (var index = 0; index < _hidden.Length; index++)
            if (_hidden[index] != null && _hidden[index].forceRenderingOff) _hidden[index].forceRenderingOff = _previousOff[index];
        _idle = _walk = _currentClip = null;
        _animationPivot = null;
    }
}
