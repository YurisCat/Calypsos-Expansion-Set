using Entities.Player;
using Entities.Player.Behaviours;
using Items;
using Mirror;
using UnityEngine;
using _200_Dev.EnvironmentElements.Player;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal static class PingItems
{
    internal static Item? Find(NetworkIdentity identity)
    {
        var item = identity.GetComponent<Item>();
        // A corpse Tool shares its world rig's identity but lives on root.x.
        item ??= identity.GetComponent<PlayerRigConfigWorld>()?.deadRagDollControllerUpgraded?.deadPlayerTool;
        return item != null && item.netIdentity == identity ? item : null;
    }

    private static PlayerRigConfigWorld? CorpseRig(Item item)
    {
        var corpse = item.TryCast<DeadPlayerTool>();
        if (corpse == null) return null;
        var rig = corpse.netIdentity?.GetComponent<PlayerRigConfigWorld>();
        return rig != null && rig.deadRagDollControllerUpgraded?.deadPlayerTool == corpse ? rig : null;
    }

    internal static bool Eligible(Item item)
    {
        if (!item.gameObject.activeInHierarchy || item.ItemOwner != null) return false;
        if (item.TryCast<DeadPlayerTool>() == null) return true;
        // Physics can also be disabled while carrying a corpse. Life state,
        // rather than the ragdoll physics flag, distinguishes death/revival.
        var player = CorpseRig(item)?.entity?.TryCast<PlayerEntity>();
        return player != null && player.CurrentHealthSync <= 0;
    }

    // Called only when caching a newly marked item, never during drawing.
    internal static Renderer[] Renderers(Item item)
    {
        var rig = CorpseRig(item);
        if (item.TryCast<DeadPlayerTool>() != null && rig == null) return Array.Empty<Renderer>();
        // The body mesh is a sibling of the skeleton, outside the Tool subtree.
        var root = rig != null ? rig.deadRagDollControllerUpgraded.transform : item.transform;
        var result = new List<Renderer>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.TryCast<MeshRenderer>() == null && renderer.TryCast<SkinnedMeshRenderer>() == null)
                continue;
            var owner = renderer.GetComponentInParent<Item>(true);
            if (rig == null ? owner != item : owner != null && owner != item
                || renderer.GetComponentInParent<PlayerRigConfigWorld>(true) != rig) continue;
            result.Add(renderer);
        }
        return result.ToArray();
    }
}
