using Entities.Monster;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Idols;

// Game assets are borrowed, never extracted into the MOD. The eligible set is
// history of actual native spawns/queues in this board, not all possible events.
internal sealed class NativeMonsterCatalog : IDisposable
{
    private static NativeMonsterCatalog? _instance;
    private static int _references;
    private readonly Dictionary<uint, MonsterEntity> _prefabs = new();
    private readonly HashSet<uint> _entered = new();
    private IntPtr _manager, _board, _clientPrefabs;
    private int _prefabCount = -1, _scene = -1;
    private float _nextPoll;
    private bool _serverActive;
    internal static NativeMonsterCatalog Current =>
        _instance ?? throw new InvalidOperationException("Monster catalog is not registered.");

    internal static IDisposable Acquire()
    {
        _instance ??= new NativeMonsterCatalog();
        _references++;
        return new Lease();
    }
    private sealed class Lease : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (--_references == 0) { _instance?.Dispose(); _instance = null; }
        }
    }

    internal void Tick()
    {
        if (Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + 0.25f;
        EnsurePrefabs();
        if (!NetworkServer.active)
        {
            if (_serverActive) _entered.Clear();
            _serverActive = false;
            return;
        }
        _serverActive = true;
        if (!RefreshBoard()) return;
        if (Testing.TestRoomLaunch.IsActive) return;
        var entities = EntityManager.Instance;
        if (entities != null)
            foreach (var monster in entities.MonsterEntitiesSet)
                if (monster != null && monster.spawnSource == MonsterEntity.ESpawnSource.GameEvent)
                    Remember(monster);
    }

    private void EnsurePrefabs()
    {
        var manager = NetworkManager.singleton;
        var scene = SceneManager.GetActiveScene().handle;
        var count = (manager != null && manager.spawnPrefabs != null ? manager.spawnPrefabs.Count : 0) + NetworkClient.prefabs.Count;
        if (_manager == (manager != null ? manager.Pointer : IntPtr.Zero) && _prefabCount == count && _scene == scene
            && _clientPrefabs == NetworkClient.prefabs.Pointer) return;
        _prefabs.Clear();
        _manager = manager != null ? manager.Pointer : IntPtr.Zero;
        _prefabCount = count; _scene = scene;
        _clientPrefabs = NetworkClient.prefabs.Pointer;
        if (manager != null && manager.spawnPrefabs != null)
            foreach (var prefab in manager.spawnPrefabs)
            {
                var monster = prefab != null ? prefab.GetComponent<MonsterEntity>() : null;
                var id = AssetId(monster);
                if (monster != null && id != 0 && monster.GetComponent<ScoreTool>() == null) _prefabs.TryAdd(id, monster);
            }
        foreach (var pair in NetworkClient.prefabs)
        {
            var monster = pair.Value != null ? pair.Value.GetComponent<MonsterEntity>() : null;
            if (monster != null && pair.Key != 0 && monster.GetComponent<ScoreTool>() == null) _prefabs.TryAdd(pair.Key, monster);
        }
        var events = GameEventManager.Instance;
        var fallback = events != null ? events.weepingAngelPrefab : null;
        var fallbackId = AssetId(fallback);
        if (fallback != null && fallbackId != 0) _prefabs.TryAdd(fallbackId, fallback);
    }

    internal void Remember(MonsterEntity monster)
    {
        if (!NetworkServer.active || monster == null || monster.GetComponent<ScoreTool>() != null
            || Testing.TestRoomLaunch.IsActive || !RefreshBoard()) return;
        var id = AssetId(monster);
        if (id != 0 && Resolve(id) != null) _entered.Add(id);
    }

    private bool RefreshBoard()
    {
        var manager = GameEventManager.Instance;
        var board = manager?.boardConfigurationData?.Pointer ?? IntPtr.Zero;
        if (board != _board) { _entered.Clear(); _board = board; }
        if (manager != null && manager._boardCompleted) { _entered.Clear(); return false; }
        return true;
    }

    internal static uint AssetId(MonsterEntity? monster) =>
        monster != null ? monster.GetComponent<NetworkIdentity>()?.assetId ?? 0U : 0U;

    internal MonsterEntity? Resolve(uint assetId)
    {
        EnsurePrefabs();
        if (_prefabs.TryGetValue(assetId, out var prefab) && prefab != null) return prefab;
        if (NetworkClient.prefabs.TryGetValue(assetId, out var gameObject) && gameObject != null)
        {
            prefab = gameObject.GetComponent<MonsterEntity>();
            if (prefab != null && prefab.GetComponent<ScoreTool>() == null) { _prefabs[assetId] = prefab; return prefab; }
        }
        return null;
    }

    internal void Eligible(List<uint> target)
    {
        Tick();
        target.Clear();
        foreach (var id in _entered) if (Resolve(id) != null) target.Add(id);
        // Queued does not mean spawned: cancelled pending entries must disappear
        // from the pool instead of becoming permanent history.
        var events = GameEventManager.Instance;
        if (NetworkServer.active && !Testing.TestRoomLaunch.IsActive && events != null
            && !events._boardCompleted && events._spawnDelayHandles != null)
            foreach (var queued in events._spawnDelayHandles)
            {
                var monster = queued?.Entity;
                var id = AssetId(monster);
                if (monster != null && monster.GetComponent<ScoreTool>() == null && id != 0
                    && Resolve(id) != null && !target.Contains(id)) target.Add(id);
            }
        target.Sort();
        if (target.Count != 0) return;
        var fallback = GameEventManager.Instance?.weepingAngelPrefab;
        var fallbackId = AssetId(fallback);
        if (fallback != null && fallbackId != 0)
        {
            _prefabs[fallbackId] = fallback;
            target.Add(fallbackId);
        }
    }

    internal IEnumerable<MonsterEntity> AllPrefabs()
    {
        EnsurePrefabs();
        return _prefabs.Values;
    }

    internal static void RememberRespawn(MonsterEntity monster) => _instance?.Remember(monster);
    internal static void ResetSession()
    {
        if (_instance == null) return;
        _instance._entered.Clear();
        _instance._board = IntPtr.Zero;
        _instance._serverActive = false;
        _instance._nextPoll = 0;
    }

    public void Dispose()
    {
        _prefabs.Clear(); _entered.Clear();
        _serverActive = false; _manager = _board = IntPtr.Zero;
    }
}

[HarmonyPatch(typeof(MonsterEntity), nameof(MonsterEntity.OnStartServer))]
internal static class MonsterCatalogSpawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(MonsterEntity __instance)
    {
        if (__instance.spawnSource == MonsterEntity.ESpawnSource.GameEvent)
            NativeMonsterCatalog.RememberRespawn(__instance);
    }
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class MonsterCatalogSessionPatch
{
    [HarmonyPrefix] private static void Prefix() => NativeMonsterCatalog.ResetSession();
}

internal sealed class MonsterCatalogRuntime : IdolContentRuntime
{
    private IDisposable? _lease = NativeMonsterCatalog.Acquire();
    public override void Tick() { if (_lease != null) NativeMonsterCatalog.Current.Tick(); }
    public override void Dispose() { _lease?.Dispose(); _lease = null; }
}

// Capture the type before a dead native instance disappears from the world.
[HarmonyPatch(typeof(GameEventManager), nameof(GameEventManager.SrvTryRegisterRespawn))]
internal static class MonsterCatalogRespawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameEventManager __instance, MonsterEntity monster)
    {
        if (monster != null && __instance.SrvIsPendingRespawn(monster))
            NativeMonsterCatalog.RememberRespawn(monster);
    }
}
