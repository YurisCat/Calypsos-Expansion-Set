using UnityEngine;

namespace MoreIdols;

public sealed class JumpingIdolDriver : MonoBehaviour
{
    public JumpingIdolDriver(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        MoreIdolsPlugin.Instance?.Tick();
    }

    private void OnGUI()
    {
        MoreIdolsPlugin.Instance?.DebugTools.Draw();
    }
}
