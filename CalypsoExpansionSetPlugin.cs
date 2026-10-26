using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Entities.Player;
using HarmonyLib;
using Items;
using Mirror;
using CalypsoExpansionSet.Idols;
using UnityEngine;
using UnityEngine.InputSystem;
using User;
using _200_Dev.Entities;

namespace CalypsoExpansionSet;

[BepInPlugin(PluginId, PluginName, PluginVersion)]
[BepInIncompatibility(LegacyPluginId)]
[BepInProcess("Invokyr.exe")]
public sealed class CalypsoExpansionSetPlugin : BasePlugin
{
    public const string PluginId = "calypso.invokyr.expansionset";
    public const string PluginName = "Calypso's Expansion Set";
    public const string PluginVersion = "0.2.88";
    // The former plugin must not register the same content/message IDs alongside this one.
    public const string LegacyPluginId = "mhz.invokyr.moreidols";
    internal static CalypsoExpansionSetPlugin? Instance { get; private set; }
    internal IdolRegistry Registry { get; private set; } = null!;
    internal ExpansionSetSync Sync { get; private set; } = null!;
    internal IdolSpawnIntegration Spawns { get; private set; } = null!;
    internal IdolDebugTools DebugTools { get; private set; } = null!;
    internal IdolDebugTeleport Teleports { get; private set; } = null!;
    internal UI.IdolHoverInfo? HoverInfo { get; private set; }
    internal Visuals.IdolVisualSync? VisualStates { get; private set; }
    internal QualityOfLife.BoardPawnCollision? PawnCollisions { get; private set; }
    internal QualityOfLife.PermanentOldClock? PermanentClock { get; private set; }
    internal Testing.TestRoomLaunch TestRoom { get; private set; } = null!;
    internal Lobby.LobbyBoxRuntime? LobbyBox { get; private set; }
    internal QualityOfLife.RoomStatusSync? RoomStatus { get; private set; }
    internal QualityOfLife.ShopStamina? ShopStamina { get; private set; }
    internal Lobby.PlayerPresenceNames? PresenceNames { get; private set; }
    internal Curses.DisguiseCurse? Disguises { get; private set; }
    internal QualityOfLife.Pings.SharedPings? Pings { get; private set; }
    internal UI.ExpansionSettingsMenu? SettingsMenu { get; private set; }

    private ExpansionSetDriver? _driver;
    private IdolEffectRuntime? _effects;
    private Harmony? _harmony;
    private ConfigEntry<bool> _naturalSpawning = null!;
    private ConfigEntry<bool> _showIndividualIdolNames = null!;
    private ConfigEntry<bool> _showIdolAdvice = null!;
    private float _nextMaintenance;
    private bool _sessionWeightsInitialized;
    private bool _failed;

    public override void Load()
    {
        Instance = this;
        Registry = new IdolRegistry(Log);
        Registry.Register(JumpingIdol.Definition);
        // SpringIdol is temporarily retired. Keep its key/IDs reserved in its
        // source; it must not enter the natural pool, debug list or test gallery.
        Registry.Register(FovIdol.Definition);
        Registry.Register(VoiceIdol.Definition);
        Registry.Register(UpsideDownIdol.Definition);
        Registry.Register(DuckStepIdol.Definition);
        Registry.Register(ExtremeSpeedIdol.Definition);
        Registry.Register(DelayedMoveIdol.Definition);
        Registry.Register(RelayIdol.Definition);
        Registry.Register(SuspiciousIdol.Definition);
        Registry.Register(WideBodyIdol.Definition);
        Registry.Register(MonsterDisguiseIdol.Definition);
        Registry.Register(MosaicIdol.Definition);
        Registry.Register(OutlineIdol.Definition);
        Registry.Register(BigHeadFisheyeIdol.Definition);
        Registry.Register(BackstepIdol.Definition);
        Registry.Register(QuantumIdol.Definition);
        Sync = new ExpansionSetSync(Log, () => Registry.Ready, () => Registry.Manifest);
        Spawns = new IdolSpawnIntegration(this);
        _effects = new IdolEffectRuntime(this);
        _naturalSpawning = Config.Bind("Gameplay", "NaturalSpawning", true,
            "新雕像加入原版自然生成池，保留生成总数和筛选规则，不保证每局出现。修改后重启。\n"
            + "/ Add new idols to vanilla spawn pools; keep spawn counts and filters. Spawns are not guaranteed. Restart after changes.");
        _showIndividualIdolNames = Config.Bind("UI", "ShowIndividualIdolNames", false,
            "按游戏语言显示已支持原版与 MOD 雕像的名称；关闭时原版保留原名，MOD 使用对应语言的统一名称。独立于箴言开关；仅本机，修改后重启。\n"
            + "/ Show localized names for supported vanilla/MOD idols; when off, keep vanilla names and use one localized MOD name. Independent of idol advice. Local only; restart after changes.");
        _showIdolAdvice = Config.Bind("UI", "ShowIdolAdvice", true,
            "指向已支持的原版与 MOD 雕像时显示顶部箴言；不影响名称、原版教程与重要消息。仅本机，修改后重启。\n"
            + "/ Show top-screen advice for supported vanilla/MOD idols. Independent of names, vanilla tutorials and important messages. Local only; restart after changes.");
        Teleports = new IdolDebugTeleport(this);
        DebugTools = new IdolDebugTools(this);
        PawnCollisions = new QualityOfLife.BoardPawnCollision(this);
        PermanentClock = new QualityOfLife.PermanentOldClock(this);
        HoverInfo = new UI.IdolHoverInfo(this);
        VisualStates = new Visuals.IdolVisualSync(this);
        TestRoom = new Testing.TestRoomLaunch(this);
        LobbyBox = new Lobby.LobbyBoxRuntime(this);
        RoomStatus = new QualityOfLife.RoomStatusSync(this);
        ShopStamina = new QualityOfLife.ShopStamina(this);
        PresenceNames = new Lobby.PlayerPresenceNames(this);
        Disguises = new Curses.DisguiseCurse(this);
        Pings = new QualityOfLife.Pings.SharedPings(this);
        SettingsMenu = new UI.ExpansionSettingsMenu(this);
        _harmony = new Harmony(PluginId);
        try
        {
            _harmony.PatchAll(typeof(CalypsoExpansionSetPlugin).Assembly);
            _driver = AddComponent<ExpansionSetDriver>();
        }
        catch
        {
            // PatchAll can fail after installing earlier render/network hooks.
            // Remove those callbacks before disposing their partially loaded owner.
            try { _harmony.UnpatchSelf(); }
            catch (Exception cleanup) { Log.LogError($"Plugin patch rollback failed: {cleanup}"); }
            try { ReleasePlugin(); }
            catch (Exception cleanup) { Log.LogError($"Plugin load rollback failed: {cleanup}"); }
            finally { Instance = null; }
            throw;
        }
        Log.LogInfo($"{PluginName} {PluginVersion} loaded. All players need compatible MOD content. "
            + "DebugMode enables one test console: F8 by default. Enter 'spawn jumping' in its input box.");
    }

    public override bool Unload()
    {
        // Live entities still refer to the cloned configs and prefabs.
        if (NetworkServer.active || NetworkClient.active)
        {
            Log.LogWarning("Leave the network session before unloading Calypso's Expansion Set.");
            return false;
        }
        ReleasePlugin();
        return true;
    }

    private void ReleasePlugin()
    {
        SettingsMenu?.Dispose();
        Pings?.Dispose();
        Disguises?.Dispose();
        PawnCollisions?.Dispose();
        LobbyBox?.Dispose();
        PresenceNames?.Dispose();
        ShopStamina?.Dispose();
        RoomStatus?.Dispose();
        Instance = null;
        if (_driver != null) UnityEngine.Object.Destroy(_driver);
        _driver = null;
        _effects?.Dispose();
        VisualStates?.Dispose();
        HoverInfo?.Dispose();
        DebugTools.Dispose();
        TestRoom.Dispose();
        _harmony?.UnpatchSelf();
        Sync.Dispose();
        Registry.Dispose();
    }

    internal void Tick()
    {
        SettingsMenu?.Tick();
        PawnCollisions?.Tick();
        TestRoom.Tick();
        if (_failed)
        {
            // Keep test-session protection until disconnect, then restore its temporary settings.
            DebugTools.LocalMultiplayer.Tick();
            DebugTools.RunCommands.Tick();
            return;
        }
        try
        {
            if (!NetworkServer.active) _sessionWeightsInitialized = false;
            if (Time.realtimeSinceStartup >= _nextMaintenance)
            {
                _nextMaintenance = Time.realtimeSinceStartup + 1f;
                Registry.TryInitialize();
                if (Registry.Ready && NetworkServer.active)
                {
                    // Includes native save/load and spawn routes outside the natural batch hook.
                    foreach (var identity in NetworkServer.spawned.Values)
                    {
                        if (identity == null) continue;
                        var idol = identity.GetComponent<ScoreTool>();
                        if (idol != null && idol.SoItem != null && Registry.TryGet(idol.SoItem.Id, out _))
                            Sync.Track(idol, false);
                    }
                }
            }
            Registry.EnsureNetworkPrefabs();
            EnsureSessionWeights();
            Sync.Tick();
            RoomStatus?.Tick();
            ShopStamina?.Tick();
            PresenceNames?.Tick();
            Disguises?.Tick();
            Pings?.Tick();
            Registry.TickContentRuntimes();
            DebugTools.Tick();
            HoverInfo?.Tick();
            LobbyBox?.Tick();
            _effects!.Tick();
            VisualStates?.Tick();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    internal void Fail(Exception exception)
    {
        if (_failed) return;
        _failed = true;
        Pings?.Dispose();
        Disguises?.Dispose();
        _effects?.Dispose();
        Registry.StopContentRuntimes();
        VisualStates?.Dispose();
        HoverInfo?.Dispose();
        DebugTools.Cancel();
        LobbyBox?.Dispose();
        PresenceNames?.Dispose();
        ShopStamina?.Dispose();
        RoomStatus?.Dispose();
        Log.LogError($"Calypso's Expansion Set runtime disabled after a registry/network error; restart after fixing it.\n{exception}");
        // Keep registered resources alive until live entities have been destroyed.
        Sync.BlockAdmission();
    }

    internal void PrepareRegistry()
    {
        if (_failed) return;
        try { Registry.TryInitialize(); EnsureSessionWeights(); }
        catch (Exception exception) { Fail(exception); }
    }

    private void EnsureSessionWeights()
    {
        if (!NetworkServer.active) { _sessionWeightsInitialized = false; return; }
        if (!Registry.Ready || _sessionWeightsInitialized) return;
        Registry.ApplyInitialWeights();
        _sessionWeightsInitialized = true;
    }

    internal bool CanAddNaturalContent => !_failed && _naturalSpawning.Value && Registry.Ready
        && NetworkServer.active && Sync.AllReadyPeersCompatible;
    internal bool RuntimeFailed => _failed;
    internal bool ShowIndividualIdolNames => _showIndividualIdolNames.Value;
    internal bool ShowIdolAdvice => _showIdolAdvice.Value;

    internal bool LocalGameplayReady(PlayerEntity player) => !DebugTools.PanelOpen && ControlsReady(player);

    internal bool SpawnNearPlayer(string key)
    {
        if (_failed || !DebugTools.Enabled) return SpawnFailure("Debug spawn is disabled. Check the error log or set Debug.DebugMode=true and restart.");
        if (!NetworkServer.active) return SpawnFailure("Only the host can spawn test idols.");
        var isMod = Registry.TryGet(key, out var entry);
        var data = isMod ? entry.Data : DebugTools.Catalog.TryGet(key, out var original) ? original : null;
        if (!Registry.Ready || data == null)
            return SpawnFailure($"Unknown or unready idol '{key}'; use list in the F8 test console.");
        if (!Sync.AllReadyPeersCompatible) return SpawnFailure("Test spawn blocked: a ready peer has not passed content compatibility.");
        Sync.Prune();
        if (isMod ? Sync.HasDebugIdol(data.Id) : DebugTools.Catalog.HasInstance(data.Id))
            return SpawnFailure($"A test idol of type '{key}' already exists. Debug allows one per type; sell/destroy it before spawning another. Natural idols do not count.");
        var player = EntityManager.Me;
        if (player == null || !ControlsReady(player))
            return SpawnFailure("Local player/controls are not ready. Close menus and try again.");
        var manager = ItemDropManager.instance;
        if (manager == null) return SpawnFailure("ItemDropManager is not ready.");
        var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        var scale = Mathf.Abs(data.unequippedScale);
        var center = data.colliderBounds.center * scale;
        var extents = data.colliderBounds.extents * scale;
        var spacing = Mathf.Max(extents.x, extents.z) * 2f + 0.3f;
        // Match the item's physics collision layers; non-colliding zone volumes are not obstacles.
        var collisionMask = 0;
        for (var layer = 0; layer < 32; layer++)
            if (!Physics.GetIgnoreLayerCollision(data.prefab.layer, layer)) collisionMask |= 1 << layer;
        Vector3? position = null;
        var floorHits = 0;
        var blockedPositions = 0;
        for (var index = 0; index < 18; index++)
        {
            var column = index % 3;
            var side = column == 0 ? 0 : column == 1 ? 1 : -1;
            var target = player.transform.position + forward * (1.5f + index / 3 * spacing) + right * (side * spacing);
            var hits = Physics.RaycastAll(target + Vector3.up * 2f, Vector3.down, 8f,
                collisionMask, QueryTriggerInteraction.Ignore);
            RaycastHit? floor = null;
            foreach (var hit in hits)
            {
                if (hit.collider == null || Vector3.Dot(hit.normal, Vector3.up) < 0.75f
                    || hit.collider.GetComponentInParent<Item>() != null
                    || hit.collider.GetComponentInParent<Entities.Entity>() != null) continue;
                if (!floor.HasValue || hit.distance < floor.Value.distance) floor = hit;
            }
            if (!floor.HasValue) continue;
            floorHits++;
            // Keep the bounds' bottom above the floor instead of adding two unrelated offsets.
            var candidate = floor.Value.point + Vector3.up * (extents.y - center.y + 0.1f);
            if (Physics.CheckBox(candidate + center, extents * 0.9f, Quaternion.identity,
                    collisionMask, QueryTriggerInteraction.Ignore)) { blockedPositions++; continue; }
            position = candidate;
            break;
        }
        if (!position.HasValue)
            return SpawnFailure($"No spawn position: floorHits={floorHits}/18, blocked={blockedPositions}, "
                + $"itemLayer={data.prefab.layer}, collisionMask={collisionMask}, center={center}, extents={extents}. Move to an open area and retry.");
        var idol = isMod ? SpawnDebugIdol(manager, data, position.Value) : DebugTools.Catalog.Spawn(manager, data, position.Value);
        if (idol == null) return false;
        if (isMod) Sync.Track(idol, true);
        else DebugTools.Catalog.Track(data, idol);
        Log.LogInfo($"[Debug spawn] {key}: netId={idol.netId}, skin={idol.Skin}, value={idol.CurrentValue}, position={position.Value}, "
            + $"allTypeDebugCount={Sync.ServerDebugCount + DebugTools.Catalog.InstanceCount}, debugLimit=1 per type.");
        return true;
    }

    internal ScoreTool? SpawnDebugIdol(ItemDropManager manager, SOScoreTool data, Vector3 position, Quaternion? rotation = null)
    {
        var skin = data.prefab.GetComponent<ScoreTool>().Skin;
        // Direct ground spawning skips the native batch's skin/value initialization.
        if (skin == ItemDropManager.EIdolSkin.None) skin = ItemDropManager.EIdolSkin.Bronze;
        Log.LogInfo($"[Debug spawn] preparing: type={data.name}, skin={skin}, position={position}.");
        int? value = null;
        // The generated out-struct wrapper allocates 8 bytes for a 24-byte native
        // value. Read boxed table entries instead of calling TryGetIdolSkinData.
        var skins = manager.idolsScoreData;
        if (skins != null)
            for (var index = 0; index < skins.Count; index++)
            {
                var candidate = skins[index];
                if (candidate != null && candidate.skin == skin) { value = candidate.skinValue; break; }
            }
        if (!value.HasValue)
        {
            SpawnFailure($"No game skin/value data for {skin}. Wait for the scene to finish loading and retry.");
            return null;
        }
        var tool = manager.SrvSpawnToolOnGround(data, position, new Il2CppSystem.Nullable<Quaternion>(rotation ?? Quaternion.identity));
        var idol = tool != null ? tool.TryCast<ScoreTool>() : null;
        if (idol == null)
        {
            if (tool != null) NetworkServer.Destroy(tool.gameObject);
            SpawnFailure("Native spawn did not return a ScoreTool. Check the registered prefab and retry.");
            return null;
        }
        Log.LogInfo($"[Debug spawn] created: netId={idol.netId}; initializing skin={skin}, value={value.Value}.");
        try { idol.Skin = skin; idol.CurrentValue = value.Value; }
        catch { NetworkServer.Destroy(idol.gameObject); throw; }
        return idol;
    }

    private bool SpawnFailure(string message) { DebugTools.Report(message, true); return false; }

    private static bool ControlsReady(PlayerEntity player)
    {
        var inputs = InputsManager.Instance;
        var movement = player.PlayerMovement;
        var controller = player.LocomotionController;
        return Time.timeScale > 0f && inputs != null && inputs.onJump != null && inputs.onJump.enabled
            && movement != null && movement.isActiveAndEnabled && movement.isOwned
            && controller != null && controller.isActiveAndEnabled;
    }

    internal string Status => $"registry={Registry.Ready}, types={Registry.Entries.Count}, server={NetworkServer.active}, "
        + $"compatibleReadyPeers={Sync.AllReadyPeersCompatible}, clientHandshake={Sync.ClientCompatible}, "
        + $"serverDebugInstances={Sync.ServerDebugCount}, debugLimit=1 per type, serverTrackedModInstances={Sync.ServerCount}, "
        + $"naturalSpawning={_naturalSpawning.Value}, failed={_failed}";
}
