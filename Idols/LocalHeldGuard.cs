using Mirror;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Idols;

internal static class LocalHeldGuard
{
    internal static bool Ready(HeldIdolContext context, ulong configId)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = context.Holder;
        var idol = context.Idol;
        return plugin != null && !plugin.RuntimeFailed && context.GameplayReady
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.isActiveAndEnabled
            && holder.CurrentHealthSync > 0 && holder.PlayerMovement != null && holder.PlayerMovement.isOwned
            && idol != null && idol.SoItem != null && idol.SoItem.Id == configId && idol.ItemOwner == holder
            && holder.InventoryNew != null && holder.InventoryNew.InHand == idol
            && plugin.LocalGameplayReady(holder);
    }
}
