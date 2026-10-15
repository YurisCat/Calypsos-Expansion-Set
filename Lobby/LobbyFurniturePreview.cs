using Mirror;
using _200_Dev.EnvironmentElements.Furnitures;
using _200_Dev.EnvironmentElements.Shop;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyFurniturePreview : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly HashSet<EGameRoomFurniture> _added = new();
    private GameRoomFurnituresManager? _manager;
    private float _nextCheck;
    // Verified against the current native shop catalog. DOORBELL has a prefab
    // and a room anchor, but is absent from the purchasable furniture list.
    private static readonly EGameRoomFurniture[] KnownShopFurniture =
    {
        EGameRoomFurniture.RADIO, EGameRoomFurniture.WARDROBE, EGameRoomFurniture.MIRROR,
        EGameRoomFurniture.DISCO, EGameRoomFurniture.DICEBOX_FIREWORK,
        EGameRoomFurniture.DICEBOX_CUBE, EGameRoomFurniture.OLD_CLOCK
    };
    internal bool Requested { get; set; } = true;
    internal int AddedCount => _added.Count;
    internal LobbyFurniturePreview(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Tick()
    {
        if (!Requested || !_plugin.DebugTools.Enabled || !Testing.TestRoomLaunch.IsActive || !NetworkServer.active)
        { Dispose(); return; }
        if (UnityEngine.Time.realtimeSinceStartup < _nextCheck) return;
        _nextCheck = UnityEngine.Time.realtimeSinceStartup + 1f;
        var manager = GameRoomFurnituresManager.Instance;
        if (manager == null || !manager.isServer || !NetworkClient.ready) return;
        if (_manager != manager) { Dispose(); _manager = manager; }
        var catalog = ShopItemSlotsManager.Instance?._furnitureItemsData;
        var available = new HashSet<EGameRoomFurniture>();
        if (catalog != null && catalog.Count is > 0 and <= 64)
        {
            foreach (var item in catalog)
                if (item != null && item.Furniture != EGameRoomFurniture.NONE
                    && Enum.IsDefined(typeof(EGameRoomFurniture), item.Furniture)) available.Add(item.Furniture);
        }
        else available.UnionWith(KnownShopFurniture);
        foreach (var previous in _added.ToArray())
            if (!available.Contains(previous))
            { if (manager.IsFurnitureActive(previous)) manager.SrvRemoveFurniture(previous); _added.Remove(previous); }
        foreach (var furniture in available)
        {
            if (furniture == EGameRoomFurniture.NONE || manager.IsFurnitureActive(furniture)) continue;
            // Track before invoking native code, so partial initialization can be undone.
            _added.Add(furniture);
            manager.SrvAddFurniture(furniture);
            _plugin.Log.LogInfo($"[Lobby preview] enabled {furniture}; isolated test session, save writes blocked.");
        }
    }

    public void Dispose()
    {
        var manager = _manager;
        _manager = null;
        if (manager != null && NetworkServer.active)
            foreach (var value in _added)
                if (manager.IsFurnitureActive(value)) manager.SrvRemoveFurniture(value);
        _added.Clear();
    }
}
