using UnityEngine;

namespace CalypsoExpansionSet;

public sealed class JumpingIdolDriver : MonoBehaviour
{
    public JumpingIdolDriver(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        CalypsoExpansionSetPlugin.Instance?.Tick();
    }

    private void OnGUI()
    {
        CalypsoExpansionSetPlugin.Instance?.DebugTools.Draw();
    }
}
