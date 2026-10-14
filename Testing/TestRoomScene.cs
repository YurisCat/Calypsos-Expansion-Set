using Entities.Player;
using EnvironmentElements;
using Items;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using _200_Dev.Entities;
using _200_Dev.EnvironmentElements.Doors;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Testing;

internal sealed class TestRoomScene : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly List<Material> _materials = new();
    private readonly List<ScoreTool> _idols = new();
    private GameObject? _root;
    private Room? _room;
    private Door? _door;
    private Vector3 _origin;
    private Quaternion _rotation;
    private int _floorLayer;
    private Font? _font;
    private bool _lightingChanged;
    private AmbientMode _previousAmbientMode;
    private Color _previousAmbientLight;
    private bool _previousFog;
    private Light? _previousSun;
    private Light? _sun;
    private static readonly Color TestAmbient = new(0.85f, 0.88f, 0.92f);
    internal int OriginalCount { get; private set; }
    internal int ModCount { get; private set; }
    internal TestRoomViewDistance? ViewDistance { get; private set; }

    internal TestRoomScene(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Build(Room start, DoorManager doors, PlayerEntity player, TestRoomPedestal pedestal)
    {
        var lookup = SOItemLookup.Instance;
        var catalog = new List<SOScoreTool>();
        var ids = new HashSet<ulong>();
        foreach (var data in lookup.scoreTools)
        {
            if (data == null || _plugin.Registry.TryGet(data.Id, out _) || !ids.Add(data.Id)) continue;
            if (data.prefab != null && data.prefab.GetComponent<MissingPieceTool>() != null)
            {
                _plugin.Log.LogInfo($"[Test room] excluded special item {data.name}: MissingPiece is not an idol exhibit.");
                continue;
            }
            if (data.prefab != null && data.prefab.GetComponent<Entities.Monster.MonsterEntity>() != null)
            {
                _plugin.Log.LogInfo($"[Test room] excluded monster-disguised item {data.name}: the gallery must contain no monsters.");
                continue;
            }
            if (data.prefab == null || data.prefab.GetComponent<ScoreTool>() == null)
                throw new InvalidOperationException($"Original idol is not ready: {data.name} ({data.Id}).");
            catalog.Add(data);
        }
        catalog.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var modCatalog = _plugin.Registry.Entries.OrderBy(entry => entry.Definition.Key, StringComparer.Ordinal).ToArray();
        foreach (var entry in modCatalog)
            if (entry.Data == null || entry.Data.prefab == null || entry.Data.prefab.GetComponent<ScoreTool>() == null)
                throw new InvalidOperationException($"Registered MOD idol is not ready: {entry.Definition.Key}.");
        var end = TestRoomLayout.End(catalog.Count, modCatalog.Length);
        SelectEntrance(start, doors, end);
        _plugin.Log.LogInfo($"[Test room] entrance={_door!.name}, origin={_origin}, forward={_rotation * Vector3.forward}, originals={catalog.Count}.");
        _root = new GameObject("Calypso TEST gallery");
        _root.SetActive(false);
        _root.transform.SetPositionAndRotation(_origin, _rotation);

        var floor = MakeMaterial("Test floor", new Color(0.25f, 0.29f, 0.32f));
        var wall = MakeMaterial("Test walls", new Color(0.48f, 0.52f, 0.55f));
        var accent = MakeMaterial("Test markers", new Color(0.15f, 0.67f, 0.61f));
        var width = TestRoomLayout.Width;
        var entrance = TestRoomLayout.EntranceLength;
        Cube("Gallery floor", new Vector3(0, -0.25f, (end + entrance) / 2),
            new Vector3(width, 0.5f, end - entrance), floor);
        Cube("Connecting corridor floor", new Vector3(0, -0.25f, entrance / 2 - 0.25f),
            new Vector3(4.5f, 0.5f, entrance + 0.5f), floor);
        Cube("Left wall", new Vector3(-width / 2, 2.5f, (end + entrance) / 2),
            new Vector3(0.3f, 5f, end - entrance), wall);
        Cube("Right wall", new Vector3(width / 2, 2.5f, (end + entrance) / 2),
            new Vector3(0.3f, 5f, end - entrance), wall);
        Cube("Back wall", new Vector3(0, 2.5f, end), new Vector3(width, 5f, 0.3f), wall);
        Cube("Gallery ceiling", new Vector3(0, 5.2f, (end + entrance) / 2),
            new Vector3(width, 0.3f, end - entrance), wall);
        Cube("Corridor ceiling", new Vector3(0, 5.2f, entrance / 2),
            new Vector3(4.8f, 0.3f, entrance), wall);
        var frontWidth = (width - 4.5f) / 2;
        foreach (var side in new[] { -1f, 1f })
        {
            Cube("Front wall", new Vector3(side * (2.25f + frontWidth / 2), 2.5f, entrance),
                new Vector3(frontWidth, 5f, 0.3f), wall);
            Cube("Corridor wall", new Vector3(side * 2.4f, 2.5f, entrance / 2),
                new Vector3(0.3f, 5f, entrance), wall);
        }
        Cube("Entrance stripe", new Vector3(0, 0.008f, 7.5f), new Vector3(width - 1, 0.01f, 0.12f), accent, false);

        // Both components are configured while inactive, before their native Awake methods.
        var zone = _root.AddComponent<Zone>();
        var trigger = _root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 3, end / 2);
        trigger.size = new Vector3(width, 8, end);
        zone.Trigger = trigger;
        zone.ZoneType = Zone.EZoneType.Zone1;
        _root.layer = start.RoomTriggers.Count > 0 ? start.RoomTriggers[0].gameObject.layer : 0;
        _room = _root.AddComponent<Room>();
        _room.roomType = (ERoomType)0;
        _room.roomTriggers = new Il2CppSystem.Collections.Generic.List<BoxCollider>();
        _room.roomTriggers.Add(trigger);
        _room.prefabRoom = _root;
        _room.noCulling = true;
        _room.tags = start.tags;
        _room.biomes = new Il2CppSystem.Collections.Generic.List<Room.BiomeToggleData>();
        _room._parentPatrolPoint = Child("Patrol points").transform;
        _room.roomId = -172903;

        try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch { _font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(); }
        Label("CALYPSO'S EXPANSION SET\nORIGINAL IDOLS", new Vector3(0, 3.3f, end - 0.25f), 0.1f);
        Label("START ROOM", new Vector3(0, 2.8f, entrance - 0.2f), 0.06f, true);
        Label("MOD IDOLS", new Vector3(0, 2.8f, TestRoomLayout.ModStart - 2f), 0.065f);
        for (var z = 10f; z < end; z += 8f)
        {
            var lamp = Child("Gallery light");
            lamp.transform.localPosition = new Vector3(0, 4.6f, z);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 22f;
            light.intensity = 30f;
            light.cullingMask = ~0;
            light.renderingLayerMask = -1;
            light.color = new Color(0.92f, 0.96f, 1f);
        }
        var sun = _sun = Child("Test daylight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 4f;
        sun.color = Color.white;
        sun.shadows = LightShadows.None;
        sun.cullingMask = ~0;
        sun.renderingLayerMask = -1;
        sun.transform.localRotation = Quaternion.Euler(55f, -30f, 0f);
        _previousAmbientMode = RenderSettings.ambientMode;
        _previousAmbientLight = RenderSettings.ambientLight;
        _previousFog = RenderSettings.fog;
        _previousSun = RenderSettings.sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = TestAmbient;
        RenderSettings.fog = false;
        RenderSettings.sun = sun;
        _lightingChanged = true;
        for (var index = 0; index < catalog.Count; index++)
        {
            var slot = TestRoomLayout.OriginalSlot(index, catalog.Count, modCatalog.Length);
            pedestal.Create(_root.transform, _floorLayer, $"Original display {index + 1:D2}", slot.X, slot.Z);
            Label($"{index + 1:D2}  {catalog[index].name.Replace("Data-Treasure-", "").Replace("Data-", "")}",
                new Vector3(slot.X, pedestal.Height + 0.15f, slot.Z - pedestal.Depth / 2 - 0.05f), 0.03f);
        }
        for (var index = 0; index < modCatalog.Length; index++)
        {
            var slot = TestRoomLayout.ModSlot(index, modCatalog.Length);
            pedestal.Create(_root.transform, _floorLayer, $"MOD display {modCatalog[index].Definition.Key}", slot.X, slot.Z);
            // The built-in world font lacks Chinese glyphs; stable keys also match F8 commands.
            Label($"MOD {index + 1:D2}\n{modCatalog[index].Definition.Key}",
                new Vector3(slot.X, pedestal.Height + 0.15f, slot.Z - pedestal.Depth / 2 - 0.05f), 0.03f);
        }
        _root.SetActive(true);
        Physics.SyncTransforms();
        RoomManager.Instance.AddNeighbourPair(start, _room, _door);
        _door!.SrvUnLockDoor();
        var defaultDoor = _door.TryCast<DefaultDoor>();
        if (defaultDoor != null) defaultDoor.SrvOpenDoor();
        else _door.NetworkisOpen = true;
        doors.SrvStopDoorTimers();
        GameEventManager.Instance?.SrvStopPendingSpawnDelayCoroutines();
        GameEventManager.Instance?.SrvClearPendingRespawns();
        EntityManager.Instance?.SrvRemoveAllMonsters();

        for (var index = 0; index < catalog.Count; index++)
        {
            SpawnDisplay(catalog[index], TestRoomLayout.OriginalSlot(index, catalog.Count, modCatalog.Length),
                pedestal.Height, $"original {index + 1}/{catalog.Count}", false);
            OriginalCount++;
        }
        for (var index = 0; index < modCatalog.Length; index++)
        {
            SpawnDisplay(modCatalog[index].Data!, TestRoomLayout.ModSlot(index, modCatalog.Length),
                pedestal.Height, $"MOD {modCatalog[index].Definition.Key}", true);
            ModCount++;
        }
        ViewDistance = new TestRoomViewDistance(_plugin, _root, TestRoomLayout.ViewDistance(catalog.Count, modCatalog.Length));
        _plugin.Log.LogInfo($"[Test room] gallery created: originals={OriginalCount} at rear, mods={ModCount} at center; "
            + $"pedestal=SM_SocleStatue, height={pedestal.Height:F3}m; spacing={TestRoomLayout.Spacing}x{TestRoomLayout.RowSpacing}m; connected door opened.");
    }

    private void SpawnDisplay(SOScoreTool data, (float X, float Z) slot, float supportHeight, string section, bool debugMod)
    {
        var bottom = (data.colliderBounds.center.y - data.colliderBounds.extents.y) * Mathf.Abs(data.unequippedScale);
        var position = World(new Vector3(slot.X, supportHeight + 0.1f - bottom, slot.Z));
        var idol = _plugin.SpawnDebugIdol(ItemDropManager.instance, data, position, _rotation * Quaternion.Euler(0f, 180f, 0f))
            ?? throw new InvalidOperationException($"Failed to spawn {data.name}.");
        _idols.Add(idol);
        if (debugMod) _plugin.Sync.Track(idol, true);
        _plugin.Log.LogInfo($"[Test room idol] {section}: {data.name}, configId={data.Id}, netId={idol.netId}, position={position}.");
    }

    private void SelectEntrance(Room start, DoorManager doors, float end)
    {
        var center = start.RoomTriggers.Count > 0 ? start.RoomTriggers[0].bounds.center : start.transform.position;
        foreach (var door in doors.StartDoors)
        {
            if (door == null) continue;
            var doorway = door.transform.position;
            var colliders = door.ThresholdColliders;
            if (colliders != null && colliders.Length > 0 && colliders[0] != null)
                doorway = colliders[0].bounds.center;
            var direction = Vector3.ProjectOnPlane(door.transform.forward, Vector3.up).normalized;
            if (Vector3.Dot(direction, doorway - center) < 0) direction = -direction;
            if (direction.sqrMagnitude < 0.5f) continue;
            RaycastHit? floor = null;
            foreach (var hit in Physics.RaycastAll(doorway - direction * 1.2f + Vector3.up * 2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.normal.y < 0.8f || hit.collider.GetComponentInParent<Item>() != null
                    || hit.collider.GetComponentInParent<Entities.Entity>() != null || hit.collider.GetComponentInParent<Door>() != null) continue;
                if (!floor.HasValue || hit.distance < floor.Value.distance) floor = hit;
            }
            if (!floor.HasValue) continue;
            var origin = new Vector3(doorway.x, floor.Value.point.y, doorway.z);
            var rotation = Quaternion.LookRotation(direction);
            var roomCenter = origin + direction * ((end + TestRoomLayout.EntranceLength) / 2) + Vector3.up * 2.5f;
            if (Physics.CheckBox(roomCenter, new Vector3(TestRoomLayout.Width / 2 - 0.3f, 2.2f,
                    (end - TestRoomLayout.EntranceLength) / 2 - 0.3f), rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                _plugin.Log.LogInfo($"[Test room] entrance {door.name} is obstructed outside; trying the next door.");
                continue;
            }
            _door = door;
            _origin = origin;
            _rotation = rotation;
            _floorLayer = floor.Value.collider.gameObject.layer;
            return;
        }
        throw new InvalidOperationException("No start-room doorway has floor inside and sufficient empty space outside.");
    }

    internal void PlacePlayer(PlayerEntity player)
    {
        var movement = player.PlayerMovement;
        var capsule = movement.capsuleCollider;
        if (capsule == null) throw new InvalidOperationException("Player capsule is unavailable.");
        var bounds = capsule.bounds;
        var feetOffset = bounds.min.y - movement.transform.position.y;
        var destination = World(new Vector3(0, 0.15f - feetOffset, 8.5f));
        var center = destination + bounds.center - movement.transform.position;
        var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        var segment = Vector3.up * Mathf.Max(0, bounds.extents.y - radius);
        foreach (var obstacle in Physics.OverlapCapsule(center - segment, center + segment, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
            if (obstacle != null && obstacle.GetComponentInParent<PlayerEntity>() != player)
                throw new InvalidOperationException($"Player landing space is blocked by {obstacle.name}.");
        movement.TargetTeleport(movement.connectionToClient, destination, _rotation);
        _plugin.Log.LogInfo($"[Test room] player teleported outside the start room to {destination}; health={player.CurrentHealthSync}, doorOpen={_door?.isOpen}.");
    }

    private Vector3 World(Vector3 local) => _origin + _rotation * local;
    private GameObject Child(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(_root!.transform, false);
        return child;
    }
    private Material MakeMaterial(string name, Color color)
    {
        // Addressables shaders need not be globally discoverable through Shader.Find.
        // Clone a loaded idol material, including its shader variants and defaults.
        if (!SOItemLookup.TryGetScoreTool(7331884207844971565UL, out var template) || template?.prefab == null)
            throw new InvalidOperationException("The original Score material is unavailable.");
        var renderer = template.prefab.GetComponentInChildren<Renderer>(true);
        if (renderer == null || renderer.sharedMaterial == null)
            throw new InvalidOperationException("The original Score renderer has no material.");
        var material = new Material(renderer.sharedMaterial) { name = "Calypso " + name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_AlbedoColor")) material.SetColor("_AlbedoColor", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_BaseMapSec")) material.SetTexture("_BaseMapSec", Texture2D.whiteTexture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        // SG_VHS_Default's _Smoothness is a PACKED TEXTURE, not the usual scalar.
        // Zero its normal/reflection contribution instead of interpreting its channels.
        foreach (var key in new[] { "_NormalStrenght", "_Reflection", "_TexLerp", "_USEBIOME", "_INNER", "_BEVELDECAL", "_USEDAMAGEFEEDBACK" })
            if (material.HasProperty(key)) material.SetFloat(key, 0f);
        if (material.HasProperty("_AO")) material.SetFloat("_AO", 1f);
        if (material.HasProperty("_MaskMap")) material.SetTexture("_MaskMap", Texture2D.whiteTexture);
        if (material.HasProperty("_USEMASKMAP")) material.SetFloat("_USEMASKMAP", 1f);
        material.EnableKeyword("_USEMASKMAP_ON");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
        if (material.HasProperty("_EmissionIntensity")) material.SetFloat("_EmissionIntensity", 2f);
        _plugin.Log.LogInfo($"[Test room] own material {name}: shader={material.shader.name}; lit with all rendering layers.");
        _materials.Add(material);
        return material;
    }
    private void Cube(string name, Vector3 localPosition, Vector3 size, Material material, bool collide = true)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(_root!.transform, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = size;
        cube.layer = _floorLayer;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (!collide) cube.GetComponent<Collider>().enabled = false;
    }
    private void Label(string text, Vector3 localPosition, float size, bool reverse = false)
    {
        if (_font == null) return;
        var go = Child("Label " + text);
        go.transform.localPosition = localPosition;
        if (reverse) go.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var label = go.AddComponent<TextMesh>();
        label.font = _font;
        label.text = text;
        label.fontSize = 64;
        label.characterSize = size;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
    }
    public void Dispose()
    {
        ViewDistance?.Dispose();
        ViewDistance = null;
        foreach (var idol in _idols)
            if (idol != null && NetworkServer.active && idol.netId != 0) NetworkServer.Destroy(idol.gameObject);
        _idols.Clear();
        OriginalCount = ModCount = 0;
        if (_room != null)
        {
            var manager = RoomManager.Instance;
            if (manager != null && manager._adjacency != null)
            {
                manager._adjacency.Remove(_room);
                foreach (var list in manager._adjacency.Values)
                    for (var index = list.Count - 1; index >= 0; index--)
                        if (list[index].room == _room) list.RemoveAt(index);
            }
        }
        if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
        _root = null;
        _room = null;
        foreach (var material in _materials) if (material != null) UObject.Destroy(material);
        _materials.Clear();
        if (_lightingChanged)
        {
            if (RenderSettings.sun == _sun) RenderSettings.sun = _previousSun;
            if (RenderSettings.ambientMode == AmbientMode.Flat && RenderSettings.ambientLight == TestAmbient)
            {
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbientLight;
            }
            if (!RenderSettings.fog) RenderSettings.fog = _previousFog;
            _lightingChanged = false;
        }
        _sun = null;
    }
}
