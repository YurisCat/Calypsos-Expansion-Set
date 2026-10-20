using EnvironmentElements;
using Mirror;
using UnityEngine;
using _200_Dev.BoardGameLogic;
using _200_Dev.Entities;
using _200_Dev.FlowLogic;
using _200_Dev.EndGame;
using _200_Dev.Utility;
using _200_Dev.Utility.Runtime.NetworkManager;

namespace CalypsoExpansionSet.Testing;

internal sealed class RunDebugCommands : IDisposable
{
    private const int StartingFunds = 5000;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private VhsNetworkManager? _manager;
    private IntPtr _connection;
    private bool _prepareRound;
    private bool _prepareRules;
    private bool _roundRequested;
    private uint _completionBoardId;
    private bool _shopReadyRequested;
    private bool _fundsGranted;
    private GameEventData? _endingEvent;
    private string _roundWaitStage = "";
    private float _roundDeadline;
    internal bool Pending => _prepareRound;
    internal string Status { get; private set; } = "";
    private bool EligibleSession => !TestRoomLaunch.IsActive && NetworkServer.active
        && (LocalMultiplayer.IsActive || NetworkServer.connections.Count == 1);
    internal bool CanUse => EligibleSession && NetworkClient.ready && EntityManager.Me != null
        && NetworkClient.connection != null && VhsNetworkManager.Singleton != null
        && _plugin.DebugTools.Enabled && !_plugin.RuntimeFailed;

    internal RunDebugCommands(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    private bool SameSession => _manager != null && VhsNetworkManager.Singleton != null
        && _manager.Pointer == VhsNetworkManager.Singleton.Pointer && NetworkClient.connection != null
        && _connection == NetworkClient.connection.Pointer;

    private void BindSession()
    {
        if (SameSession) return;
        Reset();
        _manager = VhsNetworkManager.Singleton;
        _connection = NetworkClient.connection!.Pointer;
    }

    internal void Tick()
    {
        if (_manager == null) return;
        if (!NetworkServer.active || !NetworkClient.active || !SameSession) { Reset(); return; }
        TickRoundCompletion();
    }

    private void Reset()
    {
        _manager = null;
        _connection = IntPtr.Zero;
        _prepareRound = _prepareRules = _roundRequested = _shopReadyRequested = _fundsGranted = false;
        _completionBoardId = 0;
        _endingEvent = null;
        _roundWaitStage = "";
        _roundDeadline = 0f;
        Status = "";
    }

    internal void OnWorldReset(bool succeeded, Save.EPendingLoadReason reason)
    {
        if (!succeeded || _manager == null || !NetworkServer.active) return;
        _completionBoardId = 0;
        _shopReadyRequested = false;
        Status = "";
        if (reason is Save.EPendingLoadReason.None or Save.EPendingLoadReason.EndlessRetry or Save.EPendingLoadReason.CustomRetry)
            _fundsGranted = false;
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
        _plugin.Log.LogInfo($"[Run debug] Board completion waiting for {stage}.");
    }

    internal void RequestFinish(bool prepareRules = false)
    {
        if (!CanChangeRun()) return;
        BindSession();
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
        _plugin.Log.LogInfo($"[Run debug] Queued {(prepareRules ? "rules" : "finish")}: board={board?.netId}, state={board?.BoardGameState}, active={board?.SrvIsActive}, boardCompleted={GameEventManager.Instance?.BoardCompleted}, secondStepCompleted={round?._srvRoundEndSecondStepCompleted}, shopCompleted={zone?.ShopSpawnCompleted}, requireBoardInitialization={zone?.SrvRequireBoardShopInitialization}, lastCompletionBoard={_completionBoardId}.");
    }

    internal void AddFunds(int amount)
    {
        if (!CanChangeRun()) return;
        BindSession();
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
        _plugin.Log.LogInfo($"[Run debug] Shared funds: {before} -> {after} (+{amount}).");
    }

    private bool CanChangeRun()
    {
        if (CanUse) return true;
        Report(Pick("仅正常单人或本机联机的已就绪房主可执行此操作，需开启 DebugMode。",
            "Requires DebugMode and a ready host in solo play or an F8 local test."), true);
        return false;
    }

    private void TickRoundCompletion()
    {
        if (!_prepareRound) return;
        try
        {
            if (!EligibleSession || !_plugin.DebugTools.Enabled || _plugin.RuntimeFailed)
                throw new InvalidOperationException("Board completion cancelled: connection or debug tools unavailable.");
            if (Time.realtimeSinceStartup >= _roundDeadline)
                throw new TimeoutException($"Board completion timed out waiting for {_roundWaitStage}.");
            if (!NetworkServer.active || _manager!.IsLoading
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
                        _plugin.Log.LogInfo("[Run debug] Reset previous round-end completion for the current unfinished board.");
                    }
                    ZoneManager.SrvDebugEnableRunStarted();
                    events.SrvSetBoardCompleted(true);
                    // Remember the attempted board before native work: even a partial
                    // failure or timeout must not submit its score again on retry.
                    _completionBoardId = board.netId;
                    _shopReadyRequested = false;
                    // Use native scoring and post-round board replacement. Do not enter
                    // the shop: players reach it through the normal post-round doors.
                    round.SrvCompleteRoundEndSequenceSecondStep();
                    _plugin.Log.LogInfo("[Run debug] Requested native board completion; players use the normal shop door.");
                    WaitForRound("native post-round board replacement");
                    return;
                }
            }
            if (zone._shopSpawnPending || !zone.ShopSpawnCompleted)
            { WaitForRound("native shop spawn completion"); return; }
            if (!zone.SrvRequireBoardShopInitialization)
            {
                var waiting = round._allPlayersReadyToEnterShop;
                var local = NetworkServer.localConnection;
                // The explicit shortcut can miss the solo client's ready callback.
                // Submit only our own acknowledgement through the native handler:
                // it clears the wait before swapping, so a late callback cannot swap twice.
                if (round._srvAwaitingShopReady && waiting != null && waiting.Count == 1
                    && local != null && waiting.TryGetValue(local.connectionId, out var ready) && !ready)
                {
                    if (_shopReadyRequested)
                        throw new InvalidOperationException("Native shop readiness did not complete. Retry the run before using finish again.");
                    _shopReadyRequested = true;
                    round.UserCode_CmdOnLocalPlayerReadyToEnterShop__NetworkConnectionToClient(local);
                    if (!zone.SrvRequireBoardShopInitialization)
                        throw new InvalidOperationException("Native shop readiness did not replace the finished board. Retry the run before using finish again.");
                    _plugin.Log.LogInfo("[Run debug] Submitted the sole local player's missing native shop-ready acknowledgement.");
                }
                else { WaitForRound("native post-round board replacement"); return; }
            }
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
                _plugin.Log.LogInfo($"[Run debug] Prepared post-master rule selection: visit={zone.ShopVisitCount}.");
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
            _plugin.Log.LogError($"[Run debug] Failed to complete the board: {exception}");
        }
    }

    private static string Pick(string chinese, string english) => Lobby.LobbyBoxText.Pick(chinese, english);
    private void Report(string message, bool warning = false)
    {
        Status = message;
        _plugin.DebugTools.Report(message, warning);
    }
    public void Dispose() => Reset();
}
