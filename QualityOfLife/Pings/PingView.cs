using Items;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using _200_Dev.Entities.Player.Behaviours.Camera;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal sealed class PingView : IDisposable
{
    private sealed class Target
    {
        internal sealed class Part : IDisposable
        {
            internal readonly Renderer Renderer;
            private readonly MeshFilter? _filter;
            private readonly SkinnedMeshRenderer? _skin;
            private readonly Mesh? _baked;
            internal Part(Renderer renderer)
            {
                Renderer = renderer;
                _skin = renderer.TryCast<SkinnedMeshRenderer>();
                if (_skin != null) _baked = new Mesh { name = "Calypso ping skin", hideFlags = HideFlags.DontSave };
                else _filter = renderer.GetComponent<MeshFilter>();
            }
            internal void Draw(CommandBuffer commands, Material material)
            {
                if (Renderer == null || !Renderer.gameObject.activeInHierarchy) return;
                var mesh = _filter != null ? _filter.sharedMesh : _baked;
                if (mesh == null) return;
                if (_skin != null) _skin.BakeMesh(mesh, false);
                for (var i = 0; i < mesh.subMeshCount; i++)
                    commands.DrawMesh(mesh, Renderer.transform.localToWorldMatrix, material, i, 0);
            }
            public void Dispose() { if (_baked != null) UObject.Destroy(_baked); }
        }
        internal readonly Item Item;
        internal readonly List<Part> Renderers = new();
        internal Target(Item item)
        {
            Item = item;
            foreach (var renderer in PingItems.Renderers(item)) Renderers.Add(new Part(renderer));
        }
        internal Bounds Bounds()
        {
            var bounds = new Bounds(Item.transform.position, Vector3.zero);
            var first = true;
            foreach (var part in Renderers)
                if (part.Renderer != null && part.Renderer.gameObject.activeInHierarchy)
                {
                    if (first) { bounds = part.Renderer.bounds; first = false; }
                    else bounds.Encapsulate(part.Renderer.bounds);
                }
            return bounds;
        }
        internal void Release() { foreach (var part in Renderers) part.Dispose(); }
    }

    private readonly SharedPings _pings;
    private readonly Dictionary<uint, Target> _targets = new();
    private readonly HashSet<uint> _live = new();
    private readonly List<uint> _removed = new();
    private readonly List<PingEntry> _visible = new();
    private readonly Dictionary<uint, (uint Revision, float Distance)> _pinDistances = new();
    private readonly PingPostProcessing _postProcessing = new();
    private PingResources? _resources;
    private RenderTexture? _mask, _warped;
    private CommandBuffer? _commands;
    private Camera? _camera;
    private Matrix4x4 _view, _projection;
    private Vector3 _cameraPosition;
    private int _captured = -1, _rendered = -1;
    private bool _inCamera, _tracePending;

    private static readonly Color[] Palette =
    {
        new(.95f, .56f, .53f), new(.51f, .73f, .94f), new(.94f, .80f, .44f), new(.49f, .83f, .65f),
        new(.75f, .62f, .91f), new(.95f, .69f, .47f), new(.42f, .81f, .82f), new(.92f, .60f, .76f)
    };

    internal PingView(SharedPings pings) => _pings = pings;

    internal void Tick()
    {
        _live.Clear();
        foreach (var row in _pings.Rows)
        {
            if (row.Expires <= NetworkTime.time || row.Target == 0) continue;
            var item = SharedPings.ItemFor(row.Target);
            if (item == null) continue;
            _live.Add(row.Target);
            if (!_targets.TryGetValue(row.Target, out var target) || target.Item != item)
            {
                target?.Release();
                _targets[row.Target] = new Target(item);
                _pings.Trace($"Target {item.name} ({row.Target}): {_targets[row.Target].Renderers.Count} mesh parts.");
                _tracePending = true;
            }
        }
        _removed.Clear();
        foreach (var id in _targets.Keys) if (!_live.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { _targets[id].Release(); _targets.Remove(id); }
        _live.Clear();
        foreach (var row in _pings.Rows)
            if (row.Target == 0 && row.Expires > NetworkTime.time) _live.Add(row.Owner);
        _removed.Clear();
        foreach (var owner in _pinDistances.Keys) if (!_live.Contains(owner)) _removed.Add(owner);
        foreach (var owner in _removed) _pinDistances.Remove(owner);
        if (_pings.Rows.Count == 0) ReleaseMask();
    }

    private bool Ready() => _pings.ClientReady && Time.timeScale > 0
        && !Testing.TestRoomAppearanceView.IsInspecting
        && CalypsoExpansionSetPlugin.Instance?.DebugTools.PanelOpen != true;

    internal void Capture(Camera camera)
    {
        _inCamera = false;
        try
        {
            if (!Ready() || camera == null || camera != SetFovShakeData.Instance?._cam
                || !camera.isActiveAndEnabled || camera.targetTexture != null) return;
            if (_camera != camera) _pinDistances.Clear();
            _camera = camera; _view = camera.worldToCameraMatrix; _projection = camera.projectionMatrix;
            _cameraPosition = camera.transform.position;
            _captured = Time.frameCount;
            _inCamera = true;
            _postProcessing.Begin();
        }
        catch (Exception error) { _pings.Fault(error); }
    }

    internal void FinishCamera(Camera camera)
    {
        if (camera == _camera) _inCamera = false;
    }

    internal void CapturePanini(PostProcessPassRenderGraph.PaniniProjectionPassData data)
    {
        if (!_inCamera || _captured != Time.frameCount || _pings.Rows.Count == 0) return;
        try { _postProcessing.Panini(data); }
        catch (Exception error) { _pings.Fault(error); }
    }

    internal void CaptureDistortion(Material material)
    {
        if (!_inCamera || _captured != Time.frameCount || _pings.Rows.Count == 0) return;
        try { _postProcessing.Distortion(material); }
        catch (Exception error) { _pings.Fault(error); }
    }

    internal void Render(Il2CppSystem.Collections.Generic.List<Camera> cameras)
    {
        try
        {
            if (!Ready() || _pings.Rows.Count == 0 || _camera == null || cameras == null
                || !cameras.Contains(_camera) || _captured != Time.frameCount || _rendered == Time.frameCount) return;
            _rendered = Time.frameCount;
            Draw();
        }
        catch (Exception error) { _pings.Fault(error); }
    }

    private void Draw()
    {
        var width = Screen.width; var height = Screen.height;
        if (width < 1 || height < 1 || width > 16384 || height > 16384) return;
        _visible.Clear();
        foreach (var row in _pings.Rows)
            if (row.Expires > NetworkTime.time && (row.Target == 0 || SharedPings.ItemFor(row.Target) != null))
                _visible.Add(row);
        if (_visible.Count == 0) return;
        _visible.Sort(PingPresentation.Compare);
        _resources ??= new PingResources();
        EnsureMask(width, height);
        _commands ??= new CommandBuffer { name = "Calypso shared pings" };
        var commands = _commands; var material = _resources.Material;
        var previous = RenderTexture.active;
        try
        {
            commands.Clear();
            var screen = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            var mask = new RenderTargetIdentifier(_mask);
            var warped = new RenderTargetIdentifier(_warped);
            var outline = _postProcessing.HasPanini ? warped : mask;
            var scale = Mathf.Max(1f, height / 1080f);
            commands.SetGlobalVector("_CESPingScreen", new Vector4(width, height, scale * 2, scale * 18));
            // The mask is a texture; its GPU projection differs from the final backbuffer.
            commands.SetGlobalMatrix("_CESPingVP", GL.GetGPUProjectionMatrix(_projection, true) * _view);
            _postProcessing.Configure(commands);
            if (_tracePending)
            {
                _pings.Trace($"Camera {_camera!.name}, {width}x{height}: {_postProcessing.Description}.");
                _tracePending = false;
            }
            _live.Clear();
            foreach (var row in _visible)
            {
                // Newest first, with one complete outline/arrow per item.
                // The authoritative rows retain every owner's independent ping.
                if (row.Target != 0 && !_live.Add(row.Target)) continue;
                var position = SharedPings.Vector(row.Point);
                Target? target = null;
                if (row.Target != 0)
                {
                    if (!_targets.TryGetValue(row.Target, out target)) continue;
                    position = target.Bounds().center;
                }
                var clip = _projection * (_view * new Vector4(position.x, position.y, position.z, 1));
                var w = Mathf.Max(.0001f, Mathf.Abs(clip.w));
                var point = PingProjection.Place(clip.x / w, clip.y / w, clip.w, width, height, scale * 32);
                var iconScale = 1f; var alpha = 1f;
                if (target == null)
                {
                    var distance = Vector3.Distance(_cameraPosition, position);
                    if (_pinDistances.TryGetValue(row.Owner, out var previousDistance) && previousDistance.Revision == row.Revision)
                        distance = PingPresentation.SmoothDistance(previousDistance.Distance, distance, Time.unscaledDeltaTime);
                    _pinDistances[row.Owner] = (row.Revision, distance);
                    if (!point.Edge) (iconScale, alpha) = PingPresentation.AtDistance(distance);
                }
                var color = row.Slot < Palette.Length ? Palette[row.Slot] : Color.HSVToRGB((row.Slot * .618034f) % 1, .45f, .95f);
                commands.SetGlobalVector("_CESPingColor", new Vector4(color.r, color.g, color.b, alpha));
                commands.SetGlobalVector("_CESPingPoint", new Vector4(point.X, point.Y, point.Edge ? 1 : 0, 0));
                if (target == null || point.Edge)
                {
                    var iconSize = scale * iconScale;
                    commands.SetGlobalVector("_CESPingScreen", new Vector4(width, height, scale * 2, iconSize * 18));
                    commands.SetGlobalVector("_CESPingRect", new Vector4(point.X - iconSize * 36, point.Y - iconSize * 36, iconSize * 72, iconSize * 72));
                    commands.Blit_Identifier(ref mask, ref screen, material, 2, Vector2.one, Vector2.zero, -1, -1);
                    continue;
                }
                commands.SetRenderTarget(mask);
                commands.ClearRenderTarget(false, true, Color.clear);
                foreach (var part in target.Renderers)
                {
                    // Explicit draws bypass normal frustum/occlusion culling.
                    part.Draw(commands, material);
                }
                if (_postProcessing.HasPanini) _postProcessing.Warp(commands, _mask!, warped);
                commands.Blit_Identifier(ref outline, ref screen, material, 1, Vector2.one, Vector2.zero, -1, -1);
            }
            Graphics.ExecuteCommandBuffer(commands);
        }
        finally { RenderTexture.active = previous; }
    }

    private void EnsureMask(int width, int height)
    {
        if (_mask != null && _mask.width == width && _mask.height == height) return;
        ReleaseMask();
        _mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "Calypso ping silhouette", hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
        if (!_mask.Create()) throw new InvalidOperationException("Cannot allocate the shared-ping mask.");
        _warped = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "Calypso ping projection", hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
        if (!_warped.Create()) throw new InvalidOperationException("Cannot allocate the shared-ping projection.");
    }

    private void ReleaseMask()
    {
        if (_mask != null) { _mask.Release(); UObject.Destroy(_mask); _mask = null; }
        if (_warped != null) { _warped.Release(); UObject.Destroy(_warped); _warped = null; }
    }

    internal void Clear()
    {
        foreach (var target in _targets.Values) target.Release();
        _targets.Clear(); _visible.Clear(); _live.Clear(); _removed.Clear(); _pinDistances.Clear();
        _camera = null; _captured = _rendered = -1;
        _inCamera = _tracePending = false;
        _postProcessing.Dispose();
        ReleaseMask(); _commands?.Clear();
    }

    public void Dispose()
    {
        Clear(); _commands?.Dispose(); _commands = null;
        _resources?.Dispose(); _resources = null;
    }
}
