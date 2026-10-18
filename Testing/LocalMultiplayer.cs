using System.Net;
using EnvironmentElements;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using _200_Dev.BoardGameLogic;
using _200_Dev.Entities;
using _200_Dev.FlowLogic;
using _200_Dev.EndGame;
using _200_Dev.Settings;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.Testing;

internal sealed class LocalMultiplayer : IDisposable
{
    internal const int StartingFunds = 5000;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Action _closePanel;
    private string _nickname = $"Local {Environment.ProcessId}";
    private string _status = "";
    private bool? _queuedHost;
    private int _requestFrame;
    private VhsNetworkManager? _manager;
    private bool _connected;
    private bool _connectionFailed;
    private bool _prepareRound;
    private bool _prepareRules;
    private bool _roundRequested;
    private uint _completionBoardId;
    private bool _fundsGranted;
    private GameEventData? _endingEvent;
    private string _roundWaitStage = "";
    private float _roundDeadline;
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
            "本机建房后等待所有窗口加入，再由房主输入 finish 结算；首次结算添加 5000 公共资金。其他窗口加入 127.0.0.1:7777。",
            "Wait for all windows to join, then have the host enter finish. The first request adds 5000 shared funds. Other windows join 127.0.0.1:7777."), _textStyle);
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
        var state = _prepareRound ? Pick("正在完成当前棋盘……", "Completing the current board…") : _status;
        if (state.Length == 0 && !CanStart(out var reason)) state = reason;
        if (state.Length == 0) state = Pick("未连接", "Disconnected");
        GUI.Label(new Rect(area.x, area.y + 145, area.width, 80), state, _textStyle);
        GUI.Label(new Rect(area.x, area.y + 230, area.width, 55), Pick(
            "通过原版商店买衣、换衣后再测试伪装；若诅咒已生效，请清空后重新施加，以重新采集衣服。",
            "Buy and change clothes in the normal shop before testing disguise. Clear and reapply an active curse to capture the new clothes."), _textStyle);
        GUI.Label(new Rect(area.x, area.y + 290, area.width, 90), Pick(
            "F8：finish 手动结算；rules 结算并开放大师后的规则选关。完成后自己开门去商店、选棋盘；money 5000 加钱。仅本机测试房主可用，启动资金只发一次；测试会话不写存档。",
            "F8: finish settles the board; rules also prepares post-master rule selection. Use the normal shop door and choose a board. money 5000 adds funds. Local test host only; starting funds once; no saves."), _textStyle);
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
        TickRoundCompletion();
    }

    internal void OnWorldReset(bool succeeded)
    {
        if (!succeeded || !Active || !NetworkServer.active) return;
        _completionBoardId = 0;
        if (!_prepareRound || !_roundRequested) return;
        _prepareRound = false;
        _prepareRules = false;
        Report(Pick("大厅已重新初始化，取消待处理的跳关；可使用 finish 重试。",
            "The lobby was reset; pending board completion cancelled. Use finish to retry."), true);
    }

    private void WaitForRound(string stage)
    {
        if (_roundWaitStage == stage) return;
        _roundWaitStage = stage;
        _plugin.Log.LogInfo($"[Local multiplayer] Board completion waiting for {stage}.");
    }

    internal void RequestFinish(bool prepareRules = false)
    {
        if (!CanChangeRun()) return;
        if (_prepareRound) { Report(Pick("跳关正在处理中，请等待。", "Board completion is already pending."), true); return; }
        _prepareRound = true;
        _prepareRules = prepareRules;
        var zone = ZoneManager.Instance;
        var board = BoardGameManager.Instance;
        var round = RoundEndManager.Instance;
        _roundRequested = false;
        _endingEvent = null;
        _roundWaitStage = "host scene and local player";
        _roundDeadline = Time.realtimeSinceStartup + 150f;
        _plugin.Log.LogInfo($"[Local multiplayer] Queued {(prepareRules ? "rules" : "finish")}: board={board?.netId}, state={board?.BoardGameState}, active={board?.SrvIsActive}, boardCompleted={GameEventManager.Instance?.BoardCompleted}, secondStepCompleted={round?._srvRoundEndSecondStepCompleted}, shopCompleted={zone?.ShopSpawnCompleted}, requireBoardInitialization={zone?.SrvRequireBoardShopInitialization}, lastCompletionBoard={_completionBoardId}.");
    }

    internal void AddFunds(int amount)
    {
        if (!CanChangeRun()) return;
        if (amount is < 1 or > 1000000 || Bank.instance == null)
        { Report(Pick("资金服务未就绪，或金额不在 1～1000000 之间。", "Bank is not ready, or amount is outside 1–1000000."), true); return; }
        var bank = Bank.instance;
        var before = bank.GetCurrentTotalScore();
        if (before > int.MaxValue - amount) throw new InvalidOperationException("Test funds would overflow the native bank.");
        bank.SrvAddToTotalScore(amount);
        bank.SrvSetScoreUiVisible(true);
        var after = bank.GetCurrentTotalScore();
        if (after != before + amount) throw new InvalidOperationException($"Native bank did not apply test funds: before={before}, after={after}, amount={amount}.");
        Report(Pick($"已添加 {amount} 公共资金，当前共 {after}。", $"Added {amount} shared funds; total is {after}."));
        _plugin.Log.LogInfo($"[Local multiplayer] Shared funds: {before} -> {after} (+{amount}).");
    }

    private bool CanChangeRun()
    {
        if (Active && NetworkServer.active && NetworkClient.ready && _plugin.DebugTools.Enabled && !_plugin.RuntimeFailed) return true;
        Report(Pick("仅已就绪的本机测试房主可执行此操作。", "Only the ready local test host can use this action."), true);
        return false;
    }

    private void TickRoundCompletion()
    {
        if (!Active || !_prepareRound) return;
        try
        {
            if (_connectionFailed || !_plugin.DebugTools.Enabled || _plugin.RuntimeFailed)
                throw new InvalidOperationException("Board completion cancelled: connection or debug tools unavailable.");
            if (Time.realtimeSinceStartup >= _roundDeadline)
                throw new TimeoutException($"Board completion timed out waiting for {_roundWaitStage}.");
            if (!NetworkServer.active || !_connected || _manager!.IsLoading
                || !NetworkClient.ready || EntityManager.Me == null)
            { WaitForRound("host scene and local player"); return; }
            // Fresh hosting does not call SrvResetStateP2. Use the same read-only
            // world readiness check as native save loading, without resetting the run.
            if (!_plugin.Registry.Ready || !VhsNetworkManager.IsGameWorldReadyForLoad())
            { WaitForRound("native game world"); return; }
            if (_prepareRules && !_plugin.Sync.AllReadyPeersCompatible)
            { WaitForRound("compatible ready players"); return; }
            var zone = ZoneManager.Instance;
            var bank = Bank.instance;
            var events = GameEventManager.Instance;
            var round = RoundEndManager.Instance;
            var board = BoardGameManager.Instance;
            if (zone == null || bank == null || events == null || round == null || board == null || board.netId == 0 || Flow.Instance == null)
            { WaitForRound("native round services"); return; }
            if (!_fundsGranted)
            {
                // Funding is independent of shop entry or completion callbacks.
                _fundsGranted = true;
                AddFunds(StartingFunds);
            }
            if (Flow.IsPlaying) { WaitForRound("native flow completion"); return; }
            if (!_roundRequested)
            {
                // Post-master selection can retain both the previous board's completion
                // latch and shop flags. A different, started board needs its own settlement.
                var unfinishedBoard = board.netId != _completionBoardId && board.SrvIsActive
                    && board.BoardGameState != EBoardGameState.NotStarted
                    && board.BoardGameRulesEngine != null && !board.BoardGameRulesEngine.AllAgentsHaveFinished();
                if (!unfinishedBoard && (board.netId == _completionBoardId
                    || (round._srvRoundEndSecondStepCompleted && events.BoardCompleted)
                    || (zone.ShopSpawnCompleted && zone.SrvRequireBoardShopInitialization)))
                    _roundRequested = true;
                else
                {
                    if (events.CurrentGameEventDataId != events.NoGameEventId)
                    {
                        var current = events.CurrentGameEventData;
                        if (current != null && current != _endingEvent && !events._isCurrentEventLocked)
                        {
                            _endingEvent = current;
                            current.SrvGameEventCompleted();
                        }
                        WaitForRound("current event completion");
                        return;
                    }
                    if (!zone.ShopPrefabAssigned || zone._shopSpawnPending)
                    { WaitForRound("shop prefab availability"); return; }
                    _roundRequested = true;
                    if (round._srvRoundEndSecondStepCompleted)
                    {
                        // Native first-step completion resets these fields, but requires
                        // carrying the missing piece. The explicit test shortcut skips it.
                        round._srvIsSealHeartBoardTextDisplayed = false;
                        round._srvRoundEndSecondStepCompleted = false;
                        round.SrvClearAllPlayersReady();
                        _plugin.Log.LogInfo("[Local multiplayer] Reset previous round-end completion for the current unfinished board.");
                    }
                    ZoneManager.SrvDebugEnableRunStarted();
                    events.SrvSetBoardCompleted(true);
                    // Remember the attempted board before native work: even a partial
                    // failure or timeout must not submit its score again on retry.
                    _completionBoardId = board.netId;
                    // Use native scoring and post-round board replacement. Do not enter
                    // the shop: players reach it through the normal post-round doors.
                    round.SrvCompleteRoundEndSequenceSecondStep();
                    _plugin.Log.LogInfo("[Local multiplayer] Requested native board completion; players use the normal shop door.");
                    WaitForRound("native post-round board replacement");
                    return;
                }
            }
            // The native spawn-completed callback owns board replacement.
            // SrvTrySwapRealBoardWithFake mutates the world; never poll it here.
            if (zone._shopSpawnPending || !zone.ShopSpawnCompleted || !zone.SrvRequireBoardShopInitialization)
            { WaitForRound("native post-round board replacement"); return; }
            _prepareRound = false;
            if (_prepareRules)
            {
                _prepareRules = false;
                // Wait for the shop so the native shortcut opens rule selection without entering it for players.
                var visit = zone.FirstEndlessVisit;
                if (visit <= 0) throw new InvalidOperationException("Native rule-selection visit is invalid.");
                ZoneManager.EnterEndless();
                if (zone.ShopVisitCount < visit || zone.GameModeType != ZoneManager.EGameModeType.Endless)
                    throw new InvalidOperationException("Native endless shortcut did not prepare rule selection.");
                _plugin.Log.LogInfo($"[Local multiplayer] Prepared post-master rule selection: visit={zone.ShopVisitCount}.");
                Report(Pick("已开放大师后的规则选关阶段，请正常开门去商店、选择棋盘；多人棋盘可能抽到伪装规则。",
                    "Post-master rule selection prepared. Use the shop door and choose a board; multiplayer boards may include Disguise."));
                return;
            }
            Report(Pick("棋盘已完成，请正常开门前往商店；新选棋盘放上桌并开始后，再使用 finish。",
                "Board completed. Use the normal shop door. Place and start your next board before using finish again."));
        }
        catch (Exception exception)
        {
            _prepareRound = false;
            _prepareRules = false;
            Report(Pick("跳关未完成，请查看日志；可使用 finish 重试、money 5000 手动加钱。",
                "Board completion did not finish. Check the log; use finish to retry or money 5000 to add funds."), true);
            _plugin.Log.LogError($"[Local multiplayer] Failed to complete the board: {exception}");
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
        _prepareRound = false;
        _prepareRules = false;
        _roundRequested = false;
        _completionBoardId = 0;
        _fundsGranted = false;
        _endingEvent = null;
        _roundWaitStage = "";
        _deadline = Time.realtimeSinceStartup + 90f;
        _roundDeadline = 0f;
        Active = true;
        try
        {
            Application.runInBackground = true;
            VhsNetworkManager.LastNick = _sessionNick;
            Save.ClearPendingLoad();
            Save.CurrentSaveSlot = null;
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
        _prepareRound = false;
        _prepareRules = false;
        _roundRequested = false;
        _completionBoardId = 0;
        _fundsGranted = false;
        _endingEvent = null;
        _roundWaitStage = "";
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
