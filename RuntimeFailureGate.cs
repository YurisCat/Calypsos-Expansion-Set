using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CalypsoExpansionSet;

// A failed runtime gets one new attempt after its environment changes.
// Time passing alone never reopens the gate.
internal sealed class RuntimeFailureGate
{
    private bool _blocked, _permanent;
    private int _scene;
    private IntPtr _connection, _context;
    private float _resumeAfter;

    internal bool TryResume(Object? context = null)
    {
        if (!_blocked) return true;
        if (_permanent || Time.realtimeSinceStartup < _resumeAfter) return false;
        var connection = NetworkClient.connection;
        if (SceneManager.GetActiveScene().handle == _scene
            && (connection == null || connection.Pointer == _connection)
            && (context == null || context.Pointer == _context)) return false;
        _blocked = false;
        return true;
    }

    internal void Block(Object? context = null, bool permanent = false)
    {
        if (_blocked) return;
        _scene = SceneManager.GetActiveScene().handle;
        _connection = NetworkClient.connection?.Pointer ?? IntPtr.Zero;
        _context = context != null ? context.Pointer : IntPtr.Zero;
        _resumeAfter = Time.realtimeSinceStartup + 1f;
        _permanent = permanent;
        _blocked = true;
    }

    internal void TransportReset()
    {
        if (!_permanent) _blocked = false;
    }
}
