using EnvironmentElements;
using UnityEngine;
using _200_Dev.EnvironmentElements;

namespace CalypsoExpansionSet.QualityOfLife;

internal static class RoomAreas
{
    // Use the live, oriented volumes: cached world AABBs can include space
    // outside a rotated room, or retain a shop's old position after a transition.
    internal static bool Contains(Room? room, Vector3 position)
    {
        if (room == null || !room.isActiveAndEnabled || !Finite(position)) return false;
        var triggers = room.RoomTriggers;
        if (triggers == null || triggers.Count is <= 0 or > 64) return false;
        for (var index = 0; index < triggers.Count; index++)
        {
            var trigger = triggers[index];
            if (trigger == null || !trigger.enabled || !trigger.gameObject.activeInHierarchy) continue;
            var size = trigger.size;
            var point = trigger.transform.InverseTransformPoint(position) - trigger.center;
            if (!Finite(size) || !Finite(point) || size.x <= 0 || size.y <= 0 || size.z <= 0) continue;
            if (Mathf.Abs(point.x) <= size.x * 0.5f && Mathf.Abs(point.y) <= size.y * 0.5f
                && Mathf.Abs(point.z) <= size.z * 0.5f) return true;
        }
        return false;
    }

    internal static Room? ActiveShop()
    {
        var shop = ShopRoom.Instance;
        var room = shop != null && shop.ShopVisible ? shop.ShopSpace : null;
        return room != null && (room.RoomType & ERoomType.ShopRoom) != 0 ? room : null;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
