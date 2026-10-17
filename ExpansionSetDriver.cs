using UnityEngine;

namespace CalypsoExpansionSet;

public sealed class ExpansionSetDriver : MonoBehaviour
{
    public ExpansionSetDriver(IntPtr pointer) : base(pointer)
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
