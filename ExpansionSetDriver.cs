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

    private void LateUpdate()
    {
        // Native mesh transforms must be ready before Unity prepares renderer data.
        Visuals.HeldAppearanceRuntime.RenderAll(disguisesOnly: true);
    }

    private void OnGUI()
    {
        CalypsoExpansionSetPlugin.Instance?.DebugTools.Draw();
    }
}
