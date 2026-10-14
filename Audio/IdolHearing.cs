using Mirror;

namespace CalypsoExpansionSet.Audio;

// Called only after a type's server emission has been accepted. Client playback
// never calls this path, so one shared sound produces one native hearing event.
internal static class IdolHearing
{
    internal static void Emit(HeldIdolContext context, ulong configId, float range, string diagnostic)
    {
        var holder = context.Holder;
        var idol = context.Idol;
        if (!NetworkServer.active || !context.GameplayReady || holder == null || !holder.isActiveAndEnabled
            || holder.CurrentHealthSync <= 0 || idol == null || idol.SoItem?.Id != configId
            || idol.ItemOwner != holder || holder.InventoryNew?.InHand != idol) return;
        var origin = holder.transform.position;
        if (!float.IsFinite(range) || range <= 0
            || !float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z))
            throw new ArgumentException("Invalid idol hearing position/range.");
        var hearing = HearingSenseManager.Instance
            ?? throw new InvalidOperationException("Native hearing manager is unavailable for idol noise.");
        var events = hearing.VolumeEvents;
        var before = events.Count;
        // Ordinary player source/tag, audience and native lifetime/range checks.
        hearing.AddVolumeEvent(holder.transform, range);
        if (events.Count != before + 1)
            throw new InvalidOperationException("Native hearing emission did not append exactly one event.");
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
            CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[{diagnostic} noise] idol={idol.netId}, holder={holder.netId}, "
                + $"position={origin}, range={range:F1}, nativeEvents={before}->{events.Count}, "
                + $"persistence={hearing.NoisePersistenceDuration:F2}s.");
    }
}
