using BepInEx.Configuration;
using Entities.Player;
using EnvironmentElements;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using _200_Dev.Entities;
using _200_Dev.EnvironmentElements.Doors;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.Testing;

internal sealed class TestRoomLaunch : IDisposable
{
    internal const string Argument = "--ces-test-room";
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly bool _enabled;
    private TestRoomScene? _room;
    private TestRoomPedestal? _pedestal;
    private float _nextCheck;
    private float _deadline;
    private float _settleUntil;
    private string _phase = "waiting for main menu";
    private bool _hostRequested;
    private bool _finished;
    private bool _hadSession;
    internal bool Active => _enabled;
    internal static bool IsActive => CalypsoExpansionSetPlugin.Instance?.TestRoom?.Active == true;

    internal TestRoomLaunch(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        var setting = plugin.Config.Bind("Debug", "TestRoomEnabled", false,
            "允许 --ces-test-room 专用启动入口：跳过 Logo、单人测试房、后部原版雕像与中部已注册 MOD 雕像陈列。普通启动不自动建房。");
        _enabled = setting.Value && plugin.DebugTools.Enabled
            && Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, Argument, StringComparison.Ordinal));
        plugin.Config.Save();
        if (_enabled)
        {
            TestSplashSkip.TryStop(plugin);
            _deadline = Time.realtimeSinceStartup + 150f;
            plugin.Log.LogInfo("[Test room] requested: single-player, no save writes, no events/monsters; awaiting native menu initialization.");
        }
    }

    internal void Tick()
    {
        if (!_enabled || Time.realtimeSinceStartup < _nextCheck) return;
        _nextCheck = Time.realtimeSinceStartup + 0.25f;
        if (_hadSession && !NetworkServer.active)
        {
            DisposeRoom();
            _hadSession = false;
            _finished = true;
        }
        if (_finished) return;
        try
        {
            if (_plugin.RuntimeFailed) throw new InvalidOperationException("The main plugin failed; see the preceding error.");
            if (Time.realtimeSinceStartup > _deadline)
                throw new TimeoutException($"Timed out {_phase}; restart through the Test shortcut after checking the log.");
            if (!_hostRequested)
            {
                var manager = VhsNetworkManager.Singleton;
                if (manager == null || manager.IsLoading || !SceneManager.GetSceneByName("SCN-Menu").isLoaded) return;
                if (NetworkServer.active || NetworkClient.active)
                    throw new InvalidOperationException("An existing network session was detected; automatic test startup cancelled.");
                Save.ClearPendingLoad();
                manager.maxConnections = 1;
                _hostRequested = true;
                _phase = "waiting for the local player and start room";
                manager.HostKcp();
                _plugin.Log.LogInfo("[Test room] native KCP host requested; local player only.");
                return;
            }
            if (!NetworkServer.active || !NetworkClient.ready) return;
            _hadSession = true;
            var player = EntityManager.Me;
            var doors = DoorManager.Instance;
            var start = Room.GetStartRoom();
            if (player == null || player.PlayerMovement == null || player.PlayerMovement.connectionToClient == null
                || !player.PlayerMovement.isOwned || start == null || doors == null || doors.StartDoors == null
                || doors.StartDoors.Count == 0 || Items.ItemDropManager.instance == null || !_plugin.Registry.Ready
                || !_plugin.LocalGameplayReady(player)) return;
            if (_settleUntil == 0f) { _settleUntil = Time.realtimeSinceStartup + 2f; return; }
            if (Time.realtimeSinceStartup < _settleUntil) return;
            if (_room == null)
            {
                _phase = "loading the original pedestal assets";
                _pedestal ??= new TestRoomPedestal();
                if (!_pedestal.Ready()) return;
                _phase = "creating the gallery";
                _room = new TestRoomScene(_plugin);
                _room.Build(start, doors, player, _pedestal);
                _settleUntil = Time.realtimeSinceStartup + 1f;
                return;
            }
            _phase = "placing the local player";
            _room.PlacePlayer(player);
            _finished = true;
            _plugin.Log.LogInfo($"[Test room] READY: originals={_room.OriginalCount}, mods={_room.ModCount}; no automatic respawn. F8 opens the MOD test panel.");
        }
        catch (Exception exception)
        {
            _finished = true;
            DisposeRoom();
            _plugin.Log.LogError($"[Test room] startup stopped while {_phase}:\n{exception}");
        }
    }

    private void DisposeRoom()
    {
        _room?.Dispose();
        _room = null;
        _pedestal?.Dispose();
        _pedestal = null;
    }
    public void Dispose() { _finished = true; DisposeRoom(); }
}
