using Entities.Player;
using CalypsoExpansionSet.Visuals;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.Idols;

// Move the output camera only while drawing. Native input/aim never reads the
// third-person pose back into the character's rotation or camera follow target.
internal sealed class MonsterDisguisePreview : IDisposable
{
    private static MonsterDisguisePreview? _active;
    private PlayerEntity? _holder;
    private PlayerEntity? _pickupPlayer;
    private ScoreTool? _idol;
    private ScoreTool? _pickupIdol;
    private CameraManager? _manager;
    private Camera? _view, _renderCamera;
    private Light? _renderBodyLight;
    private GameObject? _rig, _hiddenBody;
    private Renderer[] _bodyRenderers = Array.Empty<Renderer>();
    private bool[] _beforeBodyOff = Array.Empty<bool>();
    private int _bodySuppressedCount;
    private uint _waitingSequence, _monster;
    private float _until, _centerHeight, _radius;
    private bool _shown, _framed, _faulted, _returning;
    private bool _beforeBodyLightEnabled;
    private readonly LocalWorldRigView _worldRig = new();
    private readonly FovBlend _blend = new();
    private Vector3 _beforePosition, _writtenPosition;
    private Quaternion _beforeRotation, _writtenRotation;
    private Matrix4x4 _beforeProjection, _writtenProjection;
    private Camera.ProjectionMatrixMode _beforeProjectionMode;
    private float _beforeNear, _beforeFar, _writtenNear, _writtenFar;
    internal const float FieldOfView = 45f;
    internal const float TransitionSeconds = 0.25f;

    internal void Tick(uint monster, uint sequence)
    {
        if (_faulted) return;
        try { TickCore(monster, sequence); }
        catch (Exception error)
        {
            Fault(error);
        }
    }

    private void TickCore(uint monster, uint sequence)
    {
        var player = EntityManager.Me;
        HeldCameraOverrides.End();
        ReleaseRender();
        var held = HeldAppearanceRuntime.Held(player, MonsterDisguiseIdol.ConfigId, out var idol);
        if (!held) idol = null;
        if (_pickupPlayer != player || _pickupIdol != idol)
        {
            _pickupPlayer = player; _pickupIdol = idol;
            _shown = false; _waitingSequence = sequence;
        }
        var manager = CameraManager.Instance;
        if (_manager != null)
        {
            if (!_returning && monster != _monster) BeginReturn();
            if (UpdatePreview()) return;
        }
        if (!held) return;
        // Wait for a fresh authoritative snapshot after pickup; a retained row
        // from the previous equip must not preview the old random selection.
        var source = SetFovShakeData.Instance;
        var view = source?._cam;
        if (_shown || monster == 0 || sequence == _waitingSequence || manager == null
            || source == null || !source._ready || view == null || !view.isActiveAndEnabled || view.orthographic
            || source._playerPerlinShake != player!.playerPerlinShake || LocalWorldRigView.IsVisible(player)
            || player.PlayerMovement?.isOwned != true || player.WorldRigInstance == null
            || player.PlayerWorldRigController?.SkinnedMeshRenderer == null
            || player._clothChangeItemsOnWorldRig || CalypsoExpansionSetPlugin.Instance?.LocalGameplayReady(player) != true
            || !IsPov(manager)) return;
        if (!_worldRig.TryShow(player)) return;
        _holder = player; _idol = idol;
        _view = view;
        _manager = manager;
        _rig = player.WorldRigInstance;
        _bodyRenderers = _rig.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.GetComponentInParent<Items.Item>() == null).ToArray();
        _beforeBodyOff = new bool[_bodyRenderers.Length];
        _monster = monster;
        _shown = true;
        _framed = _returning = false;
        _active = this;
        _until = float.PositiveInfinity;
    }

    private static bool IsPov(CameraManager manager) =>
        manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
        || manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH;

    private bool ViewReady() => NetworkClient.isConnected && NetworkClient.ready
        && CalypsoExpansionSetPlugin.Instance?.Sync.ClientCompatible == true
        && _holder != null && _holder == EntityManager.Me
        && _holder.isActiveAndEnabled && _holder.CurrentHealthSync > 0 && _holder.PlayerMovement?.isOwned == true
        && _manager != null && _manager == CameraManager.Instance && IsPov(_manager)
        && LocalWorldRigView.IsVisible(_holder) && _holder.WorldRigInstance == _rig && !_holder._clothChangeItemsOnWorldRig
        && _view != null && _view.isActiveAndEnabled && !_view.orthographic
        && SetFovShakeData.Instance?._cam == _view && SetFovShakeData.Instance?._ready == true
        && SetFovShakeData.Instance?._playerPerlinShake == _holder.playerPerlinShake
        && CalypsoExpansionSetPlugin.Instance?.LocalGameplayReady(_holder) == true;

    private bool UpdatePreview()
    {
        // A drop or timeout can ease back while the same live POV still exists.
        // Death, menus and camera/scene replacement must release ownership now.
        if (!ViewReady()) { End(); return false; }
        var held = HeldAppearanceRuntime.Held(_holder, MonsterDisguiseIdol.ConfigId, out var idol) && idol == _idol;
        if (!held) { _pickupIdol = null; _shown = false; }
        if (!_returning && (Time.realtimeSinceStartup >= _until || !held)) BeginReturn();
        if (_returning && _blend.Complete(Time.realtimeSinceStartup)) End();
        return _manager != null;
    }

    private void BeginReturn()
    {
        if (_returning) return;
        var amount = _blend.Read(Time.realtimeSinceStartup);
        if (!_framed || amount <= 0) { End(); return; }
        _returning = true;
        _blend.Begin(amount, 0, Time.realtimeSinceStartup, TransitionSeconds);
    }

    // Enclose rotation around the player's vertical axis, including off-centre
    // meshes. Cache once: animated bounds must not pump the camera distance.
    internal static (float Height, float Radius) Measure(Bounds bounds, Vector3 origin)
    {
        var min = bounds.min - origin;
        var max = bounds.max - origin;
        var x = Math.Max(Math.Abs(min.x), Math.Abs(max.x));
        var z = Math.Max(Math.Abs(min.z), Math.Abs(max.z));
        var height = bounds.extents.y;
        var radius = MathF.Sqrt(x * x + z * z + height * height);
        if (!float.IsFinite(radius) || !float.IsFinite(bounds.center.y) || radius <= 0)
            throw new InvalidOperationException("Monster preview mesh bounds are invalid.");
        return (bounds.center.y - origin.y, Math.Max(0.05f, radius));
    }

    internal static float Distance(float radius, float aspect)
    {
        if (!float.IsFinite(aspect) || aspect <= 0)
            throw new InvalidOperationException("Monster preview camera aspect is invalid.");
        var halfAngle = FieldOfView * MathF.PI / 360;
        var limitingAngle = MathF.Atan(MathF.Tan(halfAngle) * Math.Min(1, aspect));
        return radius * 1.12f / MathF.Sin(limitingAngle);
    }

    internal static void BeginRender(Camera camera)
    {
        var preview = _active;
        if (preview == null || camera != preview._view) return;
        try
        {
            if (!preview.UpdatePreview()) return;
            preview.ApplyRender(camera);
        }
        catch (Exception error) { preview.Fault(error); }
    }

    private void ApplyRender(Camera camera)
    {
        ReleaseRender();
        var player = _holder!;
        if (!_framed)
        {
            if (!MonsterPlayerAppearance.TryGetBounds(player, out var bounds)) { End(); return; }
            (_centerHeight, _radius) = Measure(bounds, player.transform.position);
            _framed = true;
            _blend.Begin(0, 1, Time.realtimeSinceStartup, TransitionSeconds);
            _until = Time.realtimeSinceStartup + 2.25f;
        }
        var distance = Distance(_radius, camera.aspect);
        var target = player.transform.position + Vector3.up * _centerHeight;
        // Read the native processed look target, before camera shake and without
        // adding an input path: mouse, gamepad, sensitivity and inversion remain native.
        var rotation = player.playerCameraController?.CurrentPlayerCameraBehaviour?.TargetRotationFull ?? camera.transform.rotation;
        var angles = rotation.eulerAngles;
        var pitch = Mathf.Clamp(Mathf.DeltaAngle(0, angles.x) + 12f, 0, 65);
        var direction = Quaternion.Euler(pitch, angles.y, 0) * Vector3.back;
        _beforePosition = camera.transform.position;
        _beforeRotation = camera.transform.rotation;
        _beforeProjectionMode = camera.projectionMatrixMode;
        _beforeProjection = camera.projectionMatrix;
        _beforeNear = camera.nearClipPlane; _beforeFar = camera.farClipPlane;
        _writtenPosition = _beforePosition; _writtenRotation = _beforeRotation;
        _writtenProjection = _beforeProjection;
        _writtenNear = _beforeNear; _writtenFar = _beforeFar;
        _renderCamera = camera;
        var amount = _blend.Read(Time.realtimeSinceStartup);
        camera.transform.position = _writtenPosition = Vector3.Lerp(_beforePosition, target + direction * distance, amount);
        camera.transform.rotation = _writtenRotation = Quaternion.Slerp(_beforeRotation, Quaternion.LookRotation(-direction, Vector3.up), amount);
        var near = Math.Min(_beforeNear, Math.Max(0.005f, (distance - _radius) * 0.5f));
        var far = Math.Max(_beforeFar, distance + _radius + 1);
        camera.nearClipPlane = _writtenNear = _beforeNear + (near - _beforeNear) * amount;
        camera.farClipPlane = _writtenFar = _beforeFar + (far - _beforeFar) * amount;
        var projection = Matrix4x4.Perspective(FieldOfView, camera.aspect, near, far);
        for (var index = 0; index < 16; index++)
            projection[index] = _beforeProjection[index] + (projection[index] - _beforeProjection[index]) * amount;
        camera.projectionMatrix = _writtenProjection = projection;
        // During the short pull through the body, draw neither the copied
        // monster's inside surfaces nor the native body after an early drop.
        if ((_writtenPosition - target).sqrMagnitude <= _radius * _radius)
        {
            _hiddenBody = MonsterPlayerAppearance.HidePreviewBody(player);
            for (var index = 0; index < _bodyRenderers.Length; index++)
            {
                var renderer = _bodyRenderers[index];
                if (renderer == null) continue;
                _beforeBodyOff[index] = renderer.forceRenderingOff;
                _bodySuppressedCount = index + 1;
                renderer.forceRenderingOff = true;
            }
        }
        // Native free-camera body-light suppression was removed. Limit its
        // replacement to this local preview's draw, including the return blend.
        var bodyLight = player.bodyLight;
        var light = bodyLight != null ? bodyLight._light : null;
        if (amount > 0 && light != null)
        {
            _renderBodyLight = light;
            _beforeBodyLightEnabled = light.enabled;
            light.enabled = false;
        }
    }

    internal static void EndRender(Camera? camera = null)
    {
        var preview = _active;
        if (preview == null || camera != null && preview._renderCamera != camera) return;
        try { preview.ReleaseRender(); }
        catch (Exception error) { preview.Fault(error); }
    }

    private void ReleaseRender()
    {
        var light = _renderBodyLight; _renderBodyLight = null;
        if (light != null && !light.enabled) light.enabled = _beforeBodyLightEnabled;
        var body = _hiddenBody; _hiddenBody = null;
        if (body != null) body.SetActive(true);
        if (_bodySuppressedCount > 0)
        {
            var count = _bodySuppressedCount; _bodySuppressedCount = 0;
            for (var index = 0; index < count; index++)
                if (_bodyRenderers[index] != null && _bodyRenderers[index].forceRenderingOff)
                    _bodyRenderers[index].forceRenderingOff = _beforeBodyOff[index];
        }
        var camera = _renderCamera; _renderCamera = null;
        if (camera == null) return;
        if (camera.transform.position == _writtenPosition) camera.transform.position = _beforePosition;
        if (camera.transform.rotation == _writtenRotation) camera.transform.rotation = _beforeRotation;
        if (camera.nearClipPlane == _writtenNear) camera.nearClipPlane = _beforeNear;
        if (camera.farClipPlane == _writtenFar) camera.farClipPlane = _beforeFar;
        if (camera.projectionMatrixMode == Camera.ProjectionMatrixMode.Explicit && camera.projectionMatrix == _writtenProjection)
        {
            if (_beforeProjectionMode == Camera.ProjectionMatrixMode.Explicit) camera.projectionMatrix = _beforeProjection;
            else if (_beforeProjectionMode == Camera.ProjectionMatrixMode.PhysicalPropertiesBased) camera.usePhysicalProperties = true;
            else camera.ResetProjectionMatrix();
        }
    }

    private void End()
    {
        try { ReleaseRender(); }
        finally
        {
            _returning = false; _blend.Clear();
            _manager = null; _view = null; _rig = null;
            _bodyRenderers = Array.Empty<Renderer>(); _beforeBodyOff = Array.Empty<bool>();
            if (_active == this) _active = null;
            _worldRig.Dispose();
        }
    }

    private void Fault(Exception error)
    {
        _faulted = true;
        try { Dispose(); }
        catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Monster disguise preview stopped: {error}");
    }

    public void Dispose()
    {
        try { HeldCameraOverrides.End(); End(); }
        finally { _holder = _pickupPlayer = null; _idol = _pickupIdol = null; _shown = false; }
    }
}
