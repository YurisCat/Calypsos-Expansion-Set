using Entities.Monster;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using _200_Dev.Entities;
using _200_Dev.Entities.Monster.Mimic;
using _200_Dev.Entities.Monsters.CursedPlayer;

namespace CalypsoExpansionSet.Idols;

// Game assets are borrowed, never extracted into the MOD. The eligible set is
// history of actual native spawns/queues in this board, not all possible events.
internal sealed class NativeMonsterCatalog : IDisposable
{
    private static NativeMonsterCatalog? _instance;
    private static int _references;
    private readonly Dictionary<uint, MonsterEntity> _prefabs = new();
    private readonly HashSet<uint> _entered = new();
    private IntPtr _manager, _board, _clientPrefabs, _events;
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

    internal void Tick(bool force = false)
    {
        if (!force && Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + 0.25f;
        EnsurePrefabs();
        if (!NetworkServer.active)
        {
            if (_serverActive) _entered.Clear();
            _serverActive = false;
            return;
        }
        _serverActive = true;
        RefreshBoard();
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
        var events = GameEventManager.Instance;
        var eventsPointer = events != null ? events.Pointer : IntPtr.Zero;
        var scene = SceneManager.GetActiveScene().handle;
        var count = (manager != null && manager.spawnPrefabs != null ? manager.spawnPrefabs.Count : 0) + NetworkClient.prefabs.Count;
        if (_manager == (manager != null ? manager.Pointer : IntPtr.Zero) && _prefabCount == count && _scene == scene
            && _clientPrefabs == NetworkClient.prefabs.Pointer && _events == eventsPointer) return;
        _prefabs.Clear();
        _manager = manager != null ? manager.Pointer : IntPtr.Zero;
        _prefabCount = count; _scene = scene;
        _clientPrefabs = NetworkClient.prefabs.Pointer;
        _events = eventsPointer;
        if (manager != null && manager.spawnPrefabs != null)
            foreach (var prefab in manager.spawnPrefabs)
                AddPrefab(prefab);
        foreach (var pair in NetworkClient.prefabs)
            AddPrefab(pair.Value, pair.Key);
        // Native event templates can be available before Mirror's prefab lists
        // contain them. Discovering a template does not make it eligible.
        if (events != null)
        {
            AddPrefab(events.tentacleMonsterPrefab);
            AddPrefab(events.spiderPrefab);
            AddPrefab(events.jokerPrefab);
            AddPrefab(events.whiteHorsemanPrefab?.gameObject);
            AddPrefab(events.blackHorsemanPrefab?.gameObject);
            AddPrefab(events.hunterPrefab);
            AddPrefab(events.weepingAngelPrefab?.gameObject);
            AddPrefab(events.parrotPrefab);
            AddPrefab(events.yetiPrefab);
            AddPrefab(events.crocoPrefab);
            AddPrefab(events.penguinPrefab);
            AddPrefab(events.moneyGuyPrefab);
            AddPrefab(events.snowmanPrefab);
            AddPrefab(events.alpinistPrefab);
            AddPrefab(events.tvGhostPrefab);
            AddPrefab(events.pharaohPrefab);
            AddPrefab(events.beeSwarmPrefab);
            AddPrefab(events.beeHivePrefab);
            AddPrefab(events.sandWormPrefab);
        }
    }

    private MonsterEntity? AddPrefab(GameObject? prefab, uint registeredId = 0)
    {
        if (prefab == null || prefab.GetComponentInChildren<ScoreTool>(true) != null) return null;
        // Some monsters live below the network prefab root. Their entity and
        // NetworkIdentity do not have to be components on the same GameObject.
        var monster = prefab.GetComponentInChildren<MonsterEntity>(true);
        if (monster == null) return null;
        var id = registeredId != 0 ? registeredId : AssetId(monster);
        if (id == 0) return null;
        _prefabs.TryAdd(id, monster);
        return monster;
    }

    internal void Remember(MonsterEntity monster)
    {
        if (!NetworkServer.active || monster == null || monster.GetComponent<ScoreTool>() != null
            || Testing.TestRoomLaunch.IsActive) return;
        RefreshBoard();
        var id = AssetId(monster);
        // Do not lose a real spawn just because its template is still loading.
        if (id != 0) _entered.Add(id);
    }

    private void RefreshBoard()
    {
        var manager = GameEventManager.Instance;
        var board = manager?.boardConfigurationData?.Pointer ?? IntPtr.Zero;
        if (board != _board) { _entered.Clear(); _board = board; }
        // Board completion can coexist with live queues; it is not a run reset.
    }

    internal static uint AssetId(MonsterEntity? monster) =>
        monster != null ? monster.GetComponentInParent<NetworkIdentity>(true)?.assetId ?? 0U : 0U;

    internal MonsterEntity? Resolve(uint assetId)
    {
        EnsurePrefabs();
        if (_prefabs.TryGetValue(assetId, out var prefab) && prefab != null) return prefab;
        if (NetworkClient.prefabs.TryGetValue(assetId, out var gameObject) && gameObject != null)
        {
            prefab = AddPrefab(gameObject, assetId);
            if (prefab != null) return prefab;
        }
        return null;
    }

    internal bool Eligible(List<uint> target)
    {
        Tick(force: true);
        target.Clear();
        foreach (var id in _entered) if (CanDisguise(Resolve(id))) target.Add(id);
        // Queued does not mean spawned: cancelled pending entries must disappear
        // from the pool instead of becoming permanent history.
        var events = GameEventManager.Instance;
        if (NetworkServer.active && !Testing.TestRoomLaunch.IsActive && events != null
            && events._spawnDelayHandles != null)
            foreach (var queued in events._spawnDelayHandles)
            {
                var monster = queued?.Entity;
                var id = AssetId(monster);
                if (monster != null && monster.GetComponent<ScoreTool>() == null && id != 0
                    && CanDisguise(Resolve(id)) && !target.Contains(id)) target.Add(id);
            }
        target.Sort();
        if (target.Count != 0) return false;
        var fallback = GameEventManager.Instance?.weepingAngelPrefab;
        var fallbackId = AssetId(fallback);
        if (CanDisguise(fallback) && fallbackId != 0)
        {
            _prefabs[fallbackId] = fallback!;
            target.Add(fallbackId);
        }
        return true;
    }

    // Keep these native templates available to the shared sound catalog, but
    // exclude player impersonation and door-bound entities from disguises.
    private static bool CanDisguise(MonsterEntity? template) => template != null
        && template.TryCast<CursedPlayer>() == null && template.TryCast<DoorMonsterEntity>() == null;

    internal IEnumerable<MonsterEntity> AllPrefabs()
    {
        EnsurePrefabs();
        return _prefabs.Values;
    }

    internal string DescribeSources()
    {
        static string Describe(MonsterEntity? monster) => monster == null ? "null"
            : $"{monster.name}(asset={AssetId(monster)}, source={monster.spawnSource})";
        var live = new List<string>();
        var entities = EntityManager.Instance;
        if (entities != null)
            foreach (var monster in entities.MonsterEntitiesSet) live.Add(Describe(monster));
        var pending = new List<string>();
        var events = GameEventManager.Instance;
        if (events?._spawnDelayHandles != null)
            foreach (var handle in events._spawnDelayHandles) pending.Add(Describe(handle?.Entity));
        return $"templates={_prefabs.Count}, remembered={_entered.Count}, boardCompleted={events?._boardCompleted}, testRoom={Testing.TestRoomLaunch.IsActive}, live=[{string.Join(", ", live)}], pending=[{string.Join(", ", pending)}]";
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
        _serverActive = false; _manager = _board = _events = IntPtr.Zero;
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

[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.SrvRegisterMonster))]
internal static class MonsterCatalogRegisterPatch
{
    [HarmonyPostfix]
    private static void Postfix(MonsterEntity monsterEntity)
    {
        if (monsterEntity != null && monsterEntity.spawnSource == MonsterEntity.ESpawnSource.GameEvent)
            NativeMonsterCatalog.RememberRespawn(monsterEntity);
    }
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.Shutdown))]
internal static class MonsterCatalogSessionPatch
{
    [HarmonyPrefix] private static void Prefix() => NativeMonsterCatalog.ResetSession();
}

[HarmonyPatch(typeof(GameEventManager), nameof(GameEventManager.SrvOnResetGame))]
internal static class MonsterCatalogGameResetPatch
{
    [HarmonyPostfix] private static void Postfix() => NativeMonsterCatalog.ResetSession();
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
