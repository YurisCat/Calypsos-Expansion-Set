using Entities.Monster;
using EnvironmentElements;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using _200_Dev.BoardGameLogic;
using _200_Dev.Entities;
using _200_Dev.FlowLogic;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet;

internal sealed class IdolDebugMonsters
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private GameObject? _yeti;
    private GameObject? _hunter;
    private IntPtr _connection;
    private IntPtr _board;
    internal bool HasYeti => _yeti != null;
    internal bool HasHunter => _hunter != null;

    internal IdolDebugMonsters(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Tick()
    {
        if (!HasYeti && !HasHunter) return;
        var connection = NetworkClient.connection;
        var board = BoardGameManager.Instance;
        if (!NetworkServer.active || connection == null || connection.Pointer != _connection
            || (board != null ? board.Pointer : IntPtr.Zero) != _board) Clear();
    }

    internal void Toggle(bool hunter)
    {
        if (!_plugin.DebugTools.Enabled || _plugin.RuntimeFailed || !NetworkServer.active || !NetworkClient.ready
            || NetworkClient.connection == null)
        { Report("Monster test tools require the host in a ready debug session."); return; }
        ref var current = ref (hunter ? ref _hunter : ref _yeti);
        var name = hunter ? "Hunter" : "Yeti";
        if (current != null)
        {
            Destroy(current);
            current = null;
            _plugin.DebugTools.Report($"Removed the manual {name}; natural monsters were not removed.");
            return;
        }
        if (Testing.TestRoomLaunch.IsActive)
        { Report("Monster tests require a normal level; the gallery disables monster spawning."); return; }
        var player = EntityManager.Me;
        var events = GameEventManager.Instance;
        var zone = ZoneManager.Instance;
        if (player == null || player.CurrentHealthSync <= 0 || !_plugin.LocalGameplayReady(player)
            || events == null || zone == null || !_plugin.Sync.AllReadyPeersCompatible)
        { Report("Close menus and use a living host in a loaded level with compatible ready players."); return; }
        if ((Flow.Instance != null && Flow.IsPlaying) || events._isCurrentEventLocked
            || zone._shopSpawnPending || zone._clientsNotReady?.Count > 0)
        { Report("Room / event transition in progress; wait before spawning a monster."); return; }
        var prefab = hunter ? events.hunterPrefab : events.yetiPrefab;
        var template = prefab != null ? prefab.GetComponent<MonsterEntity>() : null;
        var identity = prefab != null ? prefab.GetComponent<NetworkIdentity>() : null;
        var agent = prefab != null ? prefab.GetComponentInChildren<NavMeshAgent>(true) : null;
        if (prefab == null || template == null || identity == null || identity.assetId == 0 || agent == null)
        { Report($"The native {name} prefab / navigation data is not ready; retry after loading."); return; }
        var agentType = agent.agentTypeID;
        if (agentType != 0)
        { Report($"The native {name} uses navigation type {agentType}; this test tool supports the default type 0 only."); return; }
        if (!TryFindPosition(player.transform, agent, out var position))
        { Report("No reachable, clear ground 10-15m ahead. Face an open part of the level and retry; no distant fallback was used."); return; }
        GameObject? created = null;
        try
        {
            var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            created = UObject.Instantiate(prefab, position, Quaternion.LookRotation(forward));
            if (created == null) throw new InvalidOperationException("Native monster instantiation failed.");
            var monster = created.GetComponent<MonsterEntity>();
            if (monster == null) throw new InvalidOperationException("The cloned prefab has no MonsterEntity.");
            // Set before OnStartServer so debug monsters never enter the natural disguise pool.
            monster.SpawnSource = MonsterEntity.ESpawnSource.Manual;
            NetworkServer.Spawn(created);
            if (monster.netId == 0) throw new InvalidOperationException("Native monster network spawn failed.");
            events.SrvMonsterSpawnNoDelay(monster, position, MonsterEntity.ESpawnSource.Manual);
            current = created;
            _connection = NetworkClient.connection!.Pointer;
            var board = BoardGameManager.Instance;
            _board = board != null ? board.Pointer : IntPtr.Zero;
            _plugin.DebugTools.Report($"Manual {name}: netId={monster.netId}, position={position}, "
                + $"distance={Vector3.Distance(player.transform.position, position):F1}m. Native AI is unchanged; click again to remove this instance.");
        }
        catch
        {
            if (created != null) Destroy(created);
            current = null;
            throw;
        }
    }

    private static bool TryFindPosition(Transform player, NavMeshAgent agent, out Vector3 position)
    {
        position = default;
        // The filter CalculatePath overload has invalid restored IL in the current interop.
        // Both native test monsters use type 0; the integer overloads forward to native code.
        var areaMask = agent.areaMask;
        if (!NavMesh.SamplePosition(player.position, out var origin, 2f, areaMask)) return false;
        var forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.5f) return false;
        var right = Vector3.Cross(Vector3.up, forward);
        var radius = Mathf.Max(0.1f, agent.radius);
        var height = Mathf.Max(radius * 2f, agent.height);
        var mask = 0;
        for (var layer = 0; layer < 32; layer++)
            if (!Physics.GetIgnoreLayerCollision(agent.gameObject.layer, layer)) mask |= 1 << layer;
        var path = new NavMeshPath();
        for (var index = 0; index < 9; index++)
        {
            var distance = index / 3 == 0 ? 12.5f : index / 3 == 1 ? 10f : 15f;
            var side = index % 3 == 0 ? 0f : index % 3 == 1 ? -1.5f : 1.5f;
            var target = player.position + forward * distance + right * side;
            if (!NavMesh.SamplePosition(target, out var hit, 1.5f, areaMask)) continue;
            var actualDistance = Vector3.Distance(player.position, hit.position);
            if (actualDistance < 10f || actualDistance > 15f || Mathf.Abs(hit.position.y - player.position.y) > 2f
                || Vector3.Dot(hit.position - player.position, forward) < 9f) continue;
            if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out var floor, 1.5f,
                    mask, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.75f
                || floor.collider.GetComponentInParent<Items.Item>() != null
                || floor.collider.GetComponentInParent<Entities.Entity>() != null) continue;
            if (Physics.CheckCapsule(hit.position + Vector3.up * (radius + 0.05f),
                    hit.position + Vector3.up * (height - radius + 0.05f), radius * 0.95f,
                    mask, QueryTriggerInteraction.Ignore)) continue;
            if (!NavMesh.CalculatePath(origin.position, hit.position, areaMask, path)
                || path.status != NavMeshPathStatus.PathComplete) continue;
            position = hit.position;
            return true;
        }
        return false;
    }

    private static void Destroy(GameObject instance)
    {
        var identity = instance.GetComponent<NetworkIdentity>();
        if (NetworkServer.active && identity != null && identity.netId != 0) NetworkServer.Destroy(instance);
        else UObject.Destroy(instance);
    }

    internal void Clear()
    {
        if (_yeti != null) { Destroy(_yeti); _yeti = null; }
        if (_hunter != null) { Destroy(_hunter); _hunter = null; }
        _connection = _board = IntPtr.Zero;
    }

    private void Report(string message) => _plugin.DebugTools.Report(message, true);
}
