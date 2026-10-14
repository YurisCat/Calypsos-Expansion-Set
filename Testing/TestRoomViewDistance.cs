using Mirror;
using UnityEngine;

namespace CalypsoExpansionSet.Testing;

internal sealed class TestRoomViewDistance : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly GameObject _root;
    private readonly float _minimumDistance;
    private Camera? _camera;
    private float _original, _applied;
    private bool _disposed, _faulted, _logged;

    internal TestRoomViewDistance(CalypsoExpansionSetPlugin plugin, GameObject root, float minimumDistance)
    {
        _plugin = plugin;
        _root = root;
        _minimumDistance = minimumDistance;
    }

    internal void Begin(Camera camera)
    {
        if (_disposed || _faulted) return;
        try
        {
            var source = SetFovShakeData.Instance;
            if (!_plugin.DebugTools.Enabled || _plugin.RuntimeFailed || _root == null || !_root.activeInHierarchy
                || !NetworkServer.active || !NetworkClient.ready || source == null || !source._ready
                || camera == null || source._cam != camera || !camera.isActiveAndEnabled || camera.orthographic) return;
            Release();
            var original = camera.farClipPlane;
            if (!float.IsFinite(original) || original <= 0f || original >= _minimumDistance) return;
            _original = original;
            _applied = _minimumDistance;
            _camera = camera;
            // URP reads projection and culling data after BeginCameraRendering.
            camera.farClipPlane = _applied;
            if (!_logged)
            {
                _logged = true;
                _plugin.Log.LogInfo($"[Test room] view distance: camera={camera.name}, native={original:F1}m, test={_applied:F1}m; restored after rendering.");
            }
        }
        catch (Exception exception) { Fault(exception); }
    }

    internal void End(Camera? camera = null)
    {
        if (camera != null && camera != _camera) return;
        try { Release(); }
        catch (Exception exception) { Fault(exception); }
    }

    private void Release()
    {
        var camera = _camera;
        if (camera != null && camera.farClipPlane == _applied)
            camera.farClipPlane = _original;
        _camera = null;
    }

    private void Fault(Exception exception)
    {
        if (_faulted) return;
        _faulted = true;
        try { Release(); }
        catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
        _plugin.Log.LogError($"[Test room] view-distance override disabled:\n{exception}");
    }

    public void Dispose()
    {
        _disposed = true;
        End();
    }
}
