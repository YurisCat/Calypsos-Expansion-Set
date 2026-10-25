using CalypsoExpansionSet.QualityOfLife;
using Entities.Player;
using EnvironmentElements;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Items;
using UnityEngine;
using UnityEngine.AI;
using System.Diagnostics;

namespace CalypsoExpansionSet.Idols;

internal sealed class QuantumPlacement
{
    private readonly System.Random _random = new();
    private readonly NavMeshPath _path = new();
    private readonly Il2CppReferenceArray<Collider> _overlaps = new(64);
    private readonly Il2CppStructArray<RaycastHit> _rays = new(32);
    internal const int CandidateLimit = 128;
    internal const double FrameBudgetMilliseconds = 2;
    private const float MaximumRise = 0.6f;
    private const float MaximumDrop = 3f;
    private long _remainingTicks, _started;
    private int _frameAttempts;
    private bool _searching;
    internal string LastFailure { get; private set; } = "none";
    internal int Attempts { get; private set; }
    internal double SearchMilliseconds { get; private set; }

    internal void BeginFrame()
    {
        _remainingTicks = (long)(Stopwatch.Frequency * FrameBudgetMilliseconds / 1000);
        _frameAttempts = 0;
    }

    private bool OutOfTime => _searching && Stopwatch.GetTimestamp() - _started >= _remainingTicks;

    internal static bool NearHall(Room hall, Bounds bounds)
    {
        // Live room bounds deliberately over-protect rotated doorways and keep
        // every part of the idol out of the delivery room.
        var protectedBounds = bounds; protectedBounds.Expand(0.5f);
        var triggers = hall.RoomTriggers;
        if (triggers == null || triggers.Count is <= 0 or > 64) return true;
        for (var i = 0; i < triggers.Count; i++)
        {
            var trigger = triggers[i];
            if (trigger != null && trigger.enabled && trigger.gameObject.activeInHierarchy
                && trigger.bounds.Intersects(protectedBounds)) return true;
        }
        return false;
    }

    internal bool TryFind(ScoreTool idol, PlayerEntity player, Room hall, Func<Bounds, bool> hidden,
        out Vector3 destination, out Quaternion rotation)
    {
        destination = default; rotation = default; Attempts = 0; SearchMilliseconds = 0;
        if (_remainingTicks <= 0 || _frameAttempts >= CandidateLimit) return Reject("frame-search-budget");
        _started = Stopwatch.GetTimestamp(); _searching = true;
        try { return Find(idol, player, hall, hidden, out destination, out rotation); }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - _started;
            _remainingTicks -= elapsed; _searching = false;
            SearchMilliseconds = elapsed * 1000d / Stopwatch.Frequency;
        }
    }

    private bool Find(ScoreTool idol, PlayerEntity player, Room hall, Func<Bounds, bool> hidden,
        out Vector3 destination, out Quaternion rotation)
    {
        destination = default; rotation = default; Room? room = null;
        if (!QuantumView.BoundsFor(idol, out var bounds) || idol.Collider == null) return Reject("model-bounds");
        var origin = idol.transform.position;
        var yaw = (float)(_random.NextDouble() * 360);
        rotation = Quaternion.AngleAxis(yaw, Vector3.up) * idol.transform.rotation;
        var rotatedBounds = YawBounds(bounds, origin, yaw);
        foreach (var candidate in Room.Rooms)
            if (candidate != null && candidate != hall && RoomAreas.Contains(candidate, bounds.center)
                && (candidate.RoomType & ERoomType.ShopRoom) == 0) { room = candidate; break; }
        if (room == null) return Reject("idol-room");
        var capsule = player.PlayerMovement?.capsuleCollider;
        if (capsule == null || capsule.direction != 1) return Reject("player-capsule");
        if (OutOfTime) return Reject("search-time-budget");
        var itemMask = CollisionMask(idol.Collider.gameObject.layer);
        var playerMask = CollisionMask(capsule.gameObject.layer);
        if (OutOfTime) return Reject("search-time-budget");
        if (!NavMesh.SamplePosition(player.transform.position, out var start, 1.5f, -1)) return Reject("player-navmesh");
        var maximum = RoomSpan(room, bounds.center);
        var minimum = Mathf.Max(1.5f, maximum * 0.3f);
        var phase = (float)(_random.NextDouble() * Math.PI * 2);
        // Cover four radii and all directions, rather than repeatedly drawing
        // the first physically safe point inside a motionless player's view.
        for (var i = 0; i < CandidateLimit && _frameAttempts < CandidateLimit; i++)
        {
            if (OutOfTime) return Reject("search-time-budget");
            Attempts++; _frameAttempts++;
            var ring = i % 4;
            var angle = phase + i * 2.39996323f;
            var radius = Mathf.Lerp(minimum, maximum, ring / 3f);
            var sample = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            if (!SampleGround(sample, bounds.min.y, start.position.y, itemMask | playerMask, out var target)) continue;
            if (Vector3.Distance(new Vector3(origin.x, target.y, origin.z), target) > maximum)
            { LastFailure = "candidate-range"; continue; }
            target.y -= bounds.min.y - origin.y - 0.06f;
            var moved = rotatedBounds; moved.center += target - origin;
            if (!hidden(moved)) { LastFailure = "candidate-in-view"; continue; }
            if (Validate(idol, player, hall, room, origin, target, bounds, moved, itemMask, playerMask, maximum))
            { destination = target; return true; }
        }
        return false;
    }

    private bool SampleGround(Vector3 sample, float sourceBottom, float playerGround, int mask, out Vector3 point)
    {
        point = default;
        var probe = new Vector3(sample.x, sourceBottom + MaximumRise + 0.1f, sample.z);
        var depth = MaximumRise + 0.1f + MaximumDrop;
        // The first physical surface blocks every lower layer, even if its
        // navigation is missing. Never continue through it to another platform.
        if (!SurfaceBelow(probe, depth, mask, out var floor)) return false;
        if (OutOfTime) return Reject("search-time-budget");
        if (!NavMesh.SamplePosition(floor.point, out var navigation, 0.2f, -1)) return Reject("candidate-navmesh");
        if (OutOfTime) return Reject("search-time-budget");
        // Navigation may snap sideways across an edge. Recheck the first
        // surface at that position and place on its physical height.
        probe.x = navigation.position.x; probe.z = navigation.position.z;
        if (!SurfaceBelow(probe, depth, mask, out floor)) return false;
        if (Mathf.Abs(navigation.position.y - floor.point.y) > 0.1f) return Reject("candidate-surface");
        if (floor.point.y - sourceBottom > MaximumRise || sourceBottom - floor.point.y > MaximumDrop
            || Mathf.Abs(floor.point.y - playerGround) > MaximumRise) return Reject("candidate-height");
        point = new Vector3(navigation.position.x, floor.point.y, navigation.position.z);
        return true;
    }

    private bool SurfaceBelow(Vector3 probe, float depth, int mask, out RaycastHit floor)
    {
        floor = default;
        if (OutOfTime) return Reject("search-time-budget");
        // Unity rays do not detect colliders containing their origin.
        if (Physics.OverlapBoxNonAlloc(probe, new Vector3(0.02f, 0.02f, 0.02f), _overlaps,
                Quaternion.identity, mask, QueryTriggerInteraction.Ignore) != 0) return Reject("candidate-probe-occupied");
        if (OutOfTime) return Reject("search-time-budget");
        if (!Physics.Raycast(probe, Vector3.down, out floor, depth, mask, QueryTriggerInteraction.Ignore)
            || floor.normal.y < 0.9f) return Reject("candidate-surface");
        return true;
    }

    // Validation and commit run synchronously, without pending client requests.
    internal bool Validate(ScoreTool idol, PlayerEntity player, Room hall, Room room, Vector3 origin,
        Vector3 target, Bounds sourceBounds, Bounds moved, int itemMask, int playerMask,
        float maximum = float.PositiveInfinity)
    {
        if (OutOfTime) return Reject("search-time-budget");
        if (idol == null || idol.ItemOwner != null || player == null || player.CurrentHealthSync <= 0
            || room == null || !room.isActiveAndEnabled || !QuantumView.Point(target).Valid
            || Vector3.Distance(new Vector3(origin.x, target.y, origin.z), target) > maximum
            || moved.min.y - sourceBounds.min.y > MaximumRise + 0.06f
            || sourceBounds.min.y - moved.min.y > MaximumDrop) return Reject("state-or-range");
        if (NearHall(hall, moved) || !RoomAreas.Contains(room, moved.center)
            || !RoomAreas.Contains(room, sourceBounds.center)) return Reject("hall-or-room-boundary");
        var count = Physics.OverlapBoxNonAlloc(moved.center, moved.extents * 0.98f, _overlaps,
            Quaternion.identity, itemMask, QueryTriggerInteraction.Ignore);
        if (count >= _overlaps.Length) return Reject("occupied-buffer-full");
        for (var i = 0; i < count; i++)
            if (_overlaps[i] != null && !_overlaps[i].transform.IsChildOf(idol.transform)) return Reject("occupied");
        var bottom = moved.min.y;
        for (var i = 0; i < 5; i++)
        {
            if (OutOfTime) return Reject("search-time-budget");
            var offset = i == 0 ? Vector3.zero : new Vector3((i is 1 or 2 ? -1 : 1) * moved.extents.x * 0.8f,
                0, (i is 1 or 3 ? -1 : 1) * moved.extents.z * 0.8f);
            var probe = new Vector3(moved.center.x, bottom + 0.2f, moved.center.z) + offset;
            if (!Physics.Raycast(probe, Vector3.down, out var floor, 0.4f, itemMask, QueryTriggerInteraction.Ignore)
                || floor.normal.y < 0.9f || floor.collider.GetComponentInParent<Item>() != null
                || floor.collider.GetComponentInParent<Entities.Entity>() != null
                || floor.collider.GetComponentInParent<SrvLavaFloor>() != null) return Reject("unsupported-floor");
        }
        var capsule = player.PlayerMovement?.capsuleCollider;
        var interaction = player.PlayerInteraction;
        if (capsule == null || capsule.direction != 1 || interaction == null) return Reject("player-capsule");
        // Reachability uses the server's body position. A remote player's local
        // camera rig is not the source of truth for this physical clearance.
        var viewOrigin = capsule.bounds.center + Vector3.up * capsule.bounds.extents.y * 0.7f;
        if (!QuantumView.ClearRay(_rays, viewOrigin, moved.center,
                interaction._occlusionLayerMask, idol)) return Reject("blocked-from-player");
        // A short direct corridor rules out moving through a wall/closed door.
        if (!QuantumView.ClearRay(_rays, sourceBounds.center, moved.center, interaction._occlusionLayerMask, idol)) return Reject("wall-between-positions");
        var toward = Vector3.ProjectOnPlane(player.transform.position - target, Vector3.up).normalized;
        if (toward.sqrMagnitude < 0.5f) return Reject("pickup-direction");
        var pickup = moved.center + toward * (Mathf.Max(moved.extents.x, moved.extents.z) + 0.7f);
        pickup.y = bottom;
        if (OutOfTime) return Reject("search-time-budget");
        if (!NavMesh.SamplePosition(player.transform.position, out var start, 1.5f, -1)
            || !NavMesh.SamplePosition(pickup, out var standing, 0.6f, -1)
            || Mathf.Abs(standing.position.y - bottom) > 0.3f
            || !NavMesh.CalculatePath(start.position, standing.position, -1, _path)
            || _path.status != NavMeshPathStatus.PathComplete) return Reject("pickup-path");
        // Navigation can be stale after a door moves. Sweep the player's real
        // capsule along every corner of the path, then check the pickup space.
        var playerBounds = capsule.bounds;
        var radius = Mathf.Max(playerBounds.extents.x, playerBounds.extents.z);
        var height = playerBounds.size.y;
        if (radius < 0.1f || height < radius * 2) return Reject("capsule-size");
        var corners = _path.corners;
        if (corners == null || corners.Length is < 1 or > 64) return Reject("path-corners");
        for (var i = 0; i < corners.Length; i++)
        {
            if (OutOfTime) return Reject("search-time-budget");
            var foot = corners[i] + Vector3.up * 0.08f;
            var lower = foot + Vector3.up * radius;
            var upper = foot + Vector3.up * (height - radius);
            var hits = Physics.OverlapCapsuleNonAlloc(lower, upper, radius * 0.95f, _overlaps, playerMask, QueryTriggerInteraction.Ignore);
            if (hits >= _overlaps.Length) return Reject("corridor-buffer-full");
            for (var j = 0; j < hits; j++)
                if (_overlaps[j] != null && _overlaps[j].GetComponentInParent<PlayerEntity>() != player
                    && !_overlaps[j].transform.IsChildOf(idol.transform)) return Reject("corridor-occupied");
            if (i + 1 >= corners.Length) continue;
            var delta = corners[i + 1] - corners[i];
            if (Physics.CapsuleCast(lower, upper, radius * 0.9f, delta.normalized,
                    delta.magnitude, playerMask, QueryTriggerInteraction.Ignore)) return Reject("corridor-blocked");
        }
        LastFailure = "none";
        return true;
    }

    private bool Reject(string reason) { LastFailure = reason; return false; }

    internal static Bounds YawBounds(Bounds bounds, Vector3 pivot, float degrees)
    {
        // Rotate the entire source AABB, including an offset center. This may
        // overestimate the model, but cannot miss a corner after changing yaw.
        var radians = degrees * (Mathf.PI / 180);
        var sin = Mathf.Sin(radians); var cos = Mathf.Cos(radians);
        var offset = bounds.center - pivot; var extents = bounds.extents;
        return new Bounds(pivot + new Vector3(cos * offset.x + sin * offset.z, offset.y,
            -sin * offset.x + cos * offset.z), new Vector3(
                Mathf.Abs(cos) * extents.x + Mathf.Abs(sin) * extents.z,
                extents.y, Mathf.Abs(sin) * extents.x + Mathf.Abs(cos) * extents.z) * 2);
    }

    private static float RoomSpan(Room room, Vector3 position)
    {
        var triggers = room.RoomTriggers;
        if (triggers == null) return 6f;
        for (var i = 0; i < triggers.Count; i++)
        {
            var trigger = triggers[i];
            if (trigger == null || !trigger.enabled || !trigger.bounds.Contains(position)) continue;
            var scale = trigger.transform.lossyScale; var size = trigger.size;
            var x = Mathf.Abs(size.x * scale.x); var z = Mathf.Abs(size.z * scale.z);
            // The mean horizontal side length is a room-scale upper limit,
            // capped for halls/corridors. Candidates still stay in this room.
            return Mathf.Clamp((x + z) * 0.5f, 4f, 24f);
        }
        return 6f;
    }

    private static int CollisionMask(int layer)
    {
        var mask = 0;
        for (var i = 0; i < 32; i++) if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
        return mask;
    }
}
