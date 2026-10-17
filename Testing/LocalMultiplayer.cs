using System.Net;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using _200_Dev.Entities;
using _200_Dev.Settings;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.Testing;

internal sealed class LocalMultiplayer : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Action _closePanel;
    private string _nickname = $"Local {Environment.ProcessId}";
    private string _status = "";
    private bool? _queuedHost;
    private int _requestFrame;
    private VhsNetworkManager? _manager;
    private bool _connected;
    private bool _connectionFailed;
    private float _deadline;
    private bool _previousBackground;
    private string? _previousNick;
    private string? _sessionNick;
    private GUIStyle? _textStyle;
    internal bool Active { get; private set; }
    internal static bool IsActive => CalypsoExpansionSetPlugin.Instance?.DebugTools?.LocalMultiplayer.Active == true;
    internal static string? SessionNickname => IsActive
        ? CalypsoExpansionSetPlugin.Instance!.DebugTools.LocalMultiplayer._sessionNick : null;

    internal LocalMultiplayer(CalypsoExpansionSetPlugin plugin, Action closePanel)
    {
        _plugin = plugin;
        _closePanel = closePanel;
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--ces-local-instance" && int.TryParse(args[i + 1], out var slot) && slot is >= 1 and <= 3)
                _nickname = $"Local {slot}";
    }

    internal void Draw(Rect area)
    {
        _textStyle ??= new GUIStyle { wordWrap = true, fontSize = 14 };
        _textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(area.x, area.y, area.width, 50), Pick(
            "在不同游戏窗口分别建房、加入，进入正常大厅。地址固定为本机 127.0.0.1:7777。",
            "Host in one game window and join from the others. Uses the normal lobby at 127.0.0.1:7777."), _textStyle);
        GUI.Label(new Rect(area.x, area.y + 60, 100, 25), Pick("玩家昵称", "Nickname"));
        var enabled = GUI.enabled;
        try
        {
            GUI.enabled = enabled && !Active && _queuedHost == null;
            _nickname = GUI.TextField(new Rect(area.x + 105, area.y + 58, area.width - 105, 28), _nickname, 24);
            GUI.enabled = enabled && CanStart(out _);
            var width = (area.width - 10) * 0.5f;
            if (GUI.Button(new Rect(area.x, area.y + 100, width, 32), Pick("本机建房", "Host locally"))) Request(true);
            if (GUI.Button(new Rect(area.x + width + 10, area.y + 100, width, 32), Pick("加入本机", "Join localhost"))) Request(false);
        }
        finally { GUI.enabled = enabled; }
        var state = Active && NetworkClient.ready && EntityManager.Me != null
            ? Pick(NetworkServer.active ? "房主已就绪" : "客户端已就绪", NetworkServer.active ? "Host ready" : "Client ready")
            : _status;
        if (state.Length == 0 && !CanStart(out var reason)) state = reason;
        if (state.Length == 0) state = Pick("未连接", "Disconnected");
        GUI.Label(new Rect(area.x, area.y + 150, area.width, 90), state, _textStyle);
        GUI.Label(new Rect(area.x, area.y + 250, area.width, 110), Pick(
            "先让所有窗口加入，再开始游戏。切换窗口控制各自角色；测试会话不写入存档。退出使用游戏原有菜单。",
            "Join all windows before starting the game. Switch windows to control each player. Test sessions do not save. Leave through the normal game menu."), _textStyle);
    }

    private bool CanStart(out string reason)
    {
        reason = "";
        if (!_plugin.DebugTools.Enabled || _plugin.RuntimeFailed)
            reason = Pick("调试工具不可用。", "Debug tools are unavailable.");
        else if (TestRoomLaunch.IsActive)
            reason = Pick("请普通启动游戏；单人陈列房不接受其他实例。", "Launch normally; the solo gallery does not accept other instances.");
        else if (Active || _queuedHost != null || NetworkServer.active || NetworkClient.active)
            reason = Pick("请先退出当前房间或等待连接结束。", "Leave the current session or wait for the connection to finish.");
        else if (VhsNetworkManager.Singleton == null || VhsNetworkManager.Singleton.IsLoading
            || !SceneManager.GetSceneByName("SCN-Menu").isLoaded)
            reason = Pick("请等待主菜单加载完成。", "Wait for the main menu to finish loading.");
        return reason.Length == 0;
    }

    internal void Request(bool host)
    {
        if (!CanStart(out var reason)) { Report(reason, true); return; }
        var nickname = _nickname.Trim();
        if (nickname.Length == 0 || nickname.Length > 24 || nickname.Any(char.IsControl)
            || nickname.Contains('<') || nickname.Contains('>'))
        { Report(Pick("昵称需为 1～24 个普通字符。", "Use a nickname of 1–24 plain characters."), true); return; }
        _nickname = nickname;
        _queuedHost = host;
        _requestFrame = Time.frameCount;
        _closePanel();
    }

    internal void Tick()
    {
        if (_queuedHost is { } host && Time.frameCount > _requestFrame)
        {
            _queuedHost = null;
            if (!CanStart(out var reason)) { Report(reason, true); return; }
            Start(host);
        }
        if (!Active) return;
        if (_manager == null || VhsNetworkManager.Singleton == null || _manager.Pointer != VhsNetworkManager.Singleton.Pointer)
        { Release(); Report(Pick("连接管理器已更换，测试会话结束。", "Network manager changed; test session ended."), true); return; }
        if (!_connectionFailed && NetworkClient.ready && EntityManager.Me != null)
        {
            if (!_connected) Report(Pick(NetworkServer.active ? "本机房主已就绪。" : "已加入本机房间。",
                NetworkServer.active ? "Local host ready." : "Joined the local session."));
            _connected = true;
        }
        else if (!_connectionFailed && !_connected && Time.realtimeSinceStartup >= _deadline)
        {
            _connectionFailed = true;
            StopPendingConnection();
            Report(Pick("连接超时；确认房主已建房，并查看游戏错误提示或日志。", "Connection timed out. Check the host and the game's error message or log."), true);
        }
        if (!NetworkServer.active && !NetworkClient.active)
        {
            var connected = _connected;
            var failed = _connectionFailed;
            Release();
            if (connected) Report(Pick("已离开本机房间。", "Left the local session."));
            else if (!failed)
                Report(Pick("连接已结束；若未成功加入，请查看游戏错误提示或日志。", "Connection ended. If joining failed, check the game's error message or log."), true);
        }
    }

    private void Start(bool host)
    {
        _manager = VhsNetworkManager.Singleton;
        _previousBackground = Application.runInBackground;
        _previousNick = VhsNetworkManager.LastNick;
        _sessionNick = _nickname;
        _connected = false;
        _connectionFailed = false;
        _deadline = Time.realtimeSinceStartup + 90f;
        Active = true;
        try
        {
            Application.runInBackground = true;
            VhsNetworkManager.LastNick = _sessionNick;
            Save.ClearPendingLoad();
            // The native KCP helpers read these overrides synchronously. Do not persist them.
            var settings = NetworkSettings.I;
            if (settings == null) throw new InvalidOperationException("Native network settings are not ready.");
            var previousHost = settings.hostAddress;
            var previousJoin = settings.joinAddress;
            var previousPort = settings.port;
            try
            {
                settings.hostAddress = "127.0.0.1";
                settings.joinAddress = "127.0.0.1";
                settings.port = "7777";
                if (host) _manager!.HostKcp("INVOKY"); else _manager!.JoinKcp("INVOKY");
            }
            finally
            {
                settings.hostAddress = previousHost;
                settings.joinAddress = previousJoin;
                settings.port = previousPort;
            }
            Report(Pick(host ? "正在创建本机房间……" : "正在加入本机房间……",
                host ? "Starting local host…" : "Joining localhost…"));
        }
        catch (Exception exception)
        {
            _connectionFailed = true;
            StopPendingConnection();
            if (!NetworkServer.active && !NetworkClient.active) Release();
            Report(Pick("连接启动失败，请查看日志。", "Failed to start the connection; check the log."), true);
            _plugin.Log.LogError($"[Local multiplayer] {exception}");
        }
    }

    private void StopPendingConnection()
    {
        if (_manager == null || VhsNetworkManager.Singleton == null || _manager.Pointer != VhsNetworkManager.Singleton.Pointer) return;
        try
        {
            if (NetworkServer.active) _manager.StopHost();
            else if (NetworkClient.active) _manager.StopClient();
        }
        catch (Exception exception) { _plugin.Log.LogError($"[Local multiplayer] Failed to stop pending connection: {exception}"); }
    }

    private void Release()
    {
        if (!Active) return;
        Active = false;
        if (VhsNetworkManager.LastNick == _sessionNick) VhsNetworkManager.LastNick = _previousNick;
        if (Application.runInBackground) Application.runInBackground = _previousBackground;
        _sessionNick = null;
        _manager = null;
        _connected = false;
    }

    internal static bool IsLoopback(string address) => IPAddress.TryParse(address, out var ip)
        && IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
    private static string Pick(string chinese, string english) => Lobby.LobbyBoxText.Pick(chinese, english);
    private void Report(string message, bool warning = false)
    {
        _status = message;
        _plugin.DebugTools.Report(message, warning);
    }
    internal void CancelRequest() => _queuedHost = null;
    public void Dispose() { CancelRequest(); Release(); }
}
