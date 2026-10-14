using Entities.Player;
using Items;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;

namespace CalypsoExpansionSet;

internal sealed class IdolDebugTeleport
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private uint _lastNetId;
    private IntPtr _player;
    private IntPtr _connection;

    internal IdolDebugTeleport(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal bool TryTeleportNext(string? key = null)
    {
        if (!_plugin.DebugTools.Enabled || _plugin.RuntimeFailed)
            return Failure("Debug teleport is disabled. Enable DebugMode and check the error log.");
        if (!NetworkServer.active || !NetworkClient.ready || !_plugin.Registry.Ready)
            return Failure("Only the host in a ready game session can teleport to MOD idols.");
        var player = EntityManager.Me;
        var movement = player != null ? player.PlayerMovement : null;
        var connection = movement != null ? movement.connectionToClient : null;
        if (player == null || player.CurrentHealthSync <= 0 || movement == null || connection == null
            || !_plugin.LocalGameplayReady(player))
            return Failure("Local player/controls are not ready. Close other menus and retry.");
        if (_player != player.Pointer || _connection != connection.Pointer) Reset();
        _player = player.Pointer;
        _connection = connection.Pointer;

        // Rebuild only on request: destroyed/sold idols and scene changes cannot
        // leave cached Unity objects in the cycle. Include natural and debug idols.
        var idols = new List<ScoreTool>();
        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var idol = identity.GetComponent<ScoreTool>();
            if (idol != null && idol.isActiveAndEnabled && idol.netId != 0 && idol.SoItem != null
                && _plugin.Registry.TryGet(idol.SoItem.Id, out var entry)
                && (key == null || entry.Definition.Key == key)) idols.Add(idol);
        }
        idols.Sort((left, right) => left.netId.CompareTo(right.netId));
        if (idols.Count == 0) { _lastNetId = 0; return Failure("No live MOD idols in this session. Wait for natural generation or use spawn <key>."); }
        var start = idols.FindIndex(idol => idol.netId > _lastNetId);
        if (start < 0) start = 0;
        for (var offset = 0; offset < idols.Count; offset++)
        {
            var index = (start + offset) % idols.Count;
            var idol = idols[index];
            if (idol == null || idol.SoItem == null || !_plugin.Registry.TryGet(idol.SoItem.Id, out var entry)) continue;
            if (!TryFindPosition(player, idol.transform.position, out var position)) continue;
            var facing = Vector3.ProjectOnPlane(idol.transform.position - position, Vector3.up);
            var rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : player.transform.rotation;
            var netId = idol.netId;
            var message = $"[Debug teleport] {index + 1}/{idols.Count}: type={entry.Definition.Key}, "
                + $"netId={netId}, skin={idol.Skin}, value={idol.CurrentValue}, destination={position}.";
            // The native TargetRpc calls CliTeleport on this player's owning client,
            // moves its rigidbody and resets velocity; native movement sync continues.
            movement.TargetTeleport(connection, position, rotation);
            _lastNetId = netId;
            _plugin.DebugTools.Report(message);
            return true;
        }
        return Failure($"Found {idols.Count} MOD idols, but no clear standing position nearby. Move/drop obstructing items and retry.");
    }

    private static bool TryFindPosition(PlayerEntity player, Vector3 target, out Vector3 position)
    {
        position = default;
        var movement = player.PlayerMovement;
        var capsule = movement.capsuleCollider;
        if (capsule == null || capsule.direction != 1) return false;
        var bounds = capsule.bounds;
        var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        var height = bounds.size.y;
        if (radius <= 0f || height < radius * 2f) return false;
        var centerOffset = bounds.center - movement.transform.position;
        var mask = 0;
        for (var layer = 0; layer < 32; layer++)
            if (!Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, layer)) mask |= 1 << layer;
        for (var index = 0; index < 24; index++)
        {
            var angle = index % 8 * Mathf.PI / 4f;
            var distance = 1.5f + index / 8 * 0.75f;
            var around = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            var hits = Physics.RaycastAll(around + Vector3.up * 2f, Vector3.down, 6f, mask, QueryTriggerInteraction.Ignore);
            RaycastHit? floor = null;
            foreach (var hit in hits)
            {
                if (hit.collider == null || Vector3.Dot(hit.normal, Vector3.up) < 0.75f
                    || hit.collider.GetComponentInParent<Item>() != null
                    || hit.collider.GetComponentInParent<Entities.Entity>() != null) continue;
                if (!floor.HasValue || hit.distance < floor.Value.distance) floor = hit;
            }
            if (!floor.HasValue) continue;
            var center = floor.Value.point + Vector3.up * (height * 0.5f + 0.05f);
            var halfSegment = Vector3.up * (height * 0.5f - radius);
            var blocked = false;
            foreach (var obstacle in Physics.OverlapCapsule(center - halfSegment, center + halfSegment,
                         radius * 0.95f, mask, QueryTriggerInteraction.Ignore))
                if (obstacle != null && obstacle.GetComponentInParent<PlayerEntity>() != player) { blocked = true; break; }
            if (blocked) continue;
            position = center - centerOffset;
            return true;
        }
        return false;
    }

    private bool Failure(string message) { _plugin.DebugTools.Report(message, true); return false; }
    internal void Reset() { _lastNetId = 0; _player = IntPtr.Zero; _connection = IntPtr.Zero; }
}
