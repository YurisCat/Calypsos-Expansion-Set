using BoardGame.Elements;
using CalypsoExpansionSet.Curses;
using Entities.Player;
using HarmonyLib;
using Items;
using Mirror;
using UnityEngine;
using _200_Dev.BoardGameLogic;
using _200_Dev.BoardGameLogic.Curse;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Testing;

internal sealed class DebugDiceTools : IDisposable
{
    internal enum FaceKind { Normal, Blessing, Curse }
    private sealed record Choice(int Digit, FaceKind Kind, bool Disguise);
    private sealed record Request(Choice Choice, bool Remove, IntPtr Player, IntPtr Connection, int Frame, float Deadline);
    [ThreadStatic] internal static DebugDiceTools? Executing;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Action _closePanel;
    private int _digit = 3;
    private FaceKind _kind = FaceKind.Curse;
    private bool _disguise;
    private Request? _pending;
    private DiceTool? _die;
    private Choice? _liveChoice;
    private int _faceIndex;
    private ulong _dataId;
    private IntPtr _connection;
    private IntPtr _board;
    private string _status = "";
    private GUIStyle? _textStyle;

    internal DebugDiceTools(CalypsoExpansionSetPlugin plugin, Action closePanel)
    { _plugin = plugin; _closePanel = closePanel; }

    private bool Ready => _plugin.DebugTools.Enabled && !_plugin.RuntimeFailed && !TestRoomLaunch.IsActive
        && NetworkServer.active && NetworkClient.ready && NetworkClient.connection != null
        && _plugin.Registry.Ready && _plugin.Sync.AllReadyPeersCompatible;

    internal void Draw(Rect area)
    {
        _textStyle ??= new GUIStyle { wordWrap = true, fontSize = 14 };
        _textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(area.x, area.y, area.width, 55), Pick(
            "生成独立测试骰子，无需解锁。拿起后正常投到棋盘上，固定使用所选点数和类别。",
            "Spawn a separate test die without unlocking it. Pick it up and roll it onto the board for the selected number and face type."), _textStyle);
        GUI.Label(new Rect(area.x, area.y + 64, area.width, 24), Pick("点数", "Number"));
        var numberWidth = (area.width - 40) / 6;
        for (var number = 1; number <= 6; number++)
            if (GUI.Button(new Rect(area.x + (number - 1) * (numberWidth + 8), area.y + 92, numberWidth, 32),
                    _digit == number ? $"[{number}]" : number.ToString())) _digit = number;
        var kindWidth = (area.width - 16) / 3;
        for (var index = 0; index < 3; index++)
        {
            var kind = (FaceKind)index;
            var label = KindName(kind);
            if (GUI.Button(new Rect(area.x + index * (kindWidth + 8), area.y + 138, kindWidth, 32),
                    _kind == kind ? $"[{label}]" : label)) _kind = kind;
        }
        if (_kind == FaceKind.Curse)
            _disguise = GUI.Toggle(new Rect(area.x, area.y + 184, area.width, 30), _disguise,
                Pick("指定抽中伪装（至少两名玩家）；取消则随机抽取", "Force Disguise (2+ players); uncheck for a random curse"));
        var enabled = GUI.enabled;
        try
        {
            GUI.enabled = enabled && Ready && _pending == null;
            if (GUI.Button(new Rect(area.x, area.y + 230, kindWidth * 2 + 8, 36), Pick("生成测试骰子", "Spawn test die"))) Queue(remove: false);
            GUI.enabled = enabled && NetworkServer.active && _die != null && _pending == null;
            if (GUI.Button(new Rect(area.x + (kindWidth + 8) * 2, area.y + 230, kindWidth, 36), Pick("移除", "Remove"))) Queue(remove: true);
        }
        finally { GUI.enabled = enabled; }
        GUI.Label(new Rect(area.x, area.y + 280, area.width, 62), _status.Length != 0 ? _status : Pick(
            "仅房主；同时保留一颗测试骰子。生成后修改选项只影响下一颗。",
            "Host only; one test die at a time. Changing these options affects the next die."), _textStyle);
        GUI.Label(new Rect(area.x, area.y + 350, area.width, 65), Pick(
            "指定伪装只控制本测试骰子的抽取结果，后续施加、棋子移动与换局仍走原流程。",
            "Force Disguise changes only this test die's curse selection. Applying the curse, moving the pawn and advancing the round still use the existing flow."), _textStyle);
    }

    private void Queue(bool remove)
    {
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!Ready || player == null || connection == null)
        { Report(Pick("需要已就绪的房主角色与兼容玩家。", "Requires a ready host player and compatible peers."), true); return; }
        _pending = new Request(new Choice(_digit, _kind, _kind == FaceKind.Curse && _disguise), remove,
            player.Pointer, connection.Pointer, Time.frameCount, Time.realtimeSinceStartup + 5f);
        _closePanel();
    }

    internal void Tick()
    {
        if (_pending == null && _die is null) return;
        var board = BoardGameManager.Instance;
        if (!Ready || _connection != IntPtr.Zero && (NetworkClient.connection!.Pointer != _connection
            || board == null || board.Pointer != _board)) { Clear(); return; }
        if (_die == null) { _die = null; _liveChoice = null; }
        if (_pending is not { } request || Time.frameCount <= request.Frame) return;
        var player = EntityManager.Me;
        if (player == null || player.Pointer != request.Player || NetworkClient.connection!.Pointer != request.Connection
            || Time.realtimeSinceStartup > request.Deadline)
        { _pending = null; Report(Pick("测试请求已取消：角色、会话变化或超时。", "Test request cancelled: player/session changed or timed out."), true); return; }
        if (!_plugin.LocalGameplayReady(player)) return;
        _pending = null;
        try
        {
            if (request.Remove) { Clear(); Report(Pick("已移除测试骰子。", "Test die removed.")); }
            else Spawn(player, request.Choice);
        }
        catch (Exception error)
        {
            Clear();
            Report(Pick("测试骰子操作失败，请查看日志。", "Test die action failed; check the log."), true);
            _plugin.Log.LogError(error);
        }
    }

    private void Spawn(PlayerEntity player, Choice choice)
    {
        if (_die != null) { Report(Pick("已有测试骰子，请先使用或移除它。", "A test die already exists; use or remove it first."), true); return; }
        var board = BoardGameManager.Instance;
        var manager = ItemDropManager.instance;
        if (board == null || manager == null)
        { Report(Pick("请先进入正常对局。", "Enter a normal game session first."), true); return; }
        if (choice.Disguise && _plugin.Disguises?.CanRoll() != true)
        { Report(Pick("指定伪装至少需要两名兼容、已生成的玩家。", "Forcing Disguise requires at least two compatible, spawned players."), true); return; }
        var data = FindData(choice.Digit, choice.Kind, out var faceIndex);
        if (data == null)
        { Report(Pick("原版配置中找不到该骰面，请等待加载完成。", "No native die contains that face; wait for loading to finish."), true); return; }
        var position = _plugin.FindDebugSpawnPosition(data, player);
        if (!position.HasValue)
        { Report(Pick("附近没有安全生成位置，请移到开阔地面重试。", "No safe spawn position nearby; move to open ground and retry."), true); return; }
        var tool = manager.SrvSpawnToolOnGround(data, position.Value, new Il2CppSystem.Nullable<Quaternion>(Quaternion.identity));
        var die = tool != null ? tool.TryCast<DiceTool>() : null;
        if (die == null || die.DiceData == null || die.DiceData.Id != data.Id)
        {
            if (tool != null) NetworkServer.Destroy(tool.gameObject);
            throw new InvalidOperationException("Native spawn did not initialize the selected DiceTool data.");
        }
        _die = die;
        _liveChoice = choice;
        _faceIndex = faceIndex;
        _dataId = data.Id;
        _connection = NetworkClient.connection!.Pointer;
        _board = board.Pointer;
        Report(Pick($"已生成{KindName(choice.Kind)} {choice.Digit} 点测试骰子，拿起后投向棋盘。",
            $"Spawned a {KindName(choice.Kind)} {choice.Digit} test die; pick it up and roll it onto the board."));
        _plugin.Log.LogInfo($"[Debug dice] spawned: dice={die.netId}, data={data.name} ({data.Id}), faceIndex={faceIndex}, number={choice.Digit}, kind={choice.Kind}, forceDisguise={choice.Disguise}.");
    }

    internal static SODiceTool? FindData(int digit, FaceKind kind, out int faceIndex)
    {
        faceIndex = -1;
        if (digit is < 1 or > 6 || !Enum.IsDefined(typeof(FaceKind), kind)) return null;
        var lookup = SOItemLookup.Instance;
        if (lookup == null || lookup.diceTools == null) return null;
        SODiceTool? selected = null;
        var bestCount = 0;
        foreach (var data in lookup.diceTools)
        {
            if (data == null || data.Id == 0 || data.prefab == null || data.prefab.GetComponent<DiceTool>() == null) continue;
            var faces = data.diceRotationInfos;
            if (faces == null || faces.Length != 6) continue;
            var count = 0;
            var first = -1;
            for (var index = 0; index < faces.Length; index++)
            {
                var face = faces[index]?.soFaceResult;
                if (face == null || !Matches(face, digit, kind)) continue;
                if (first < 0) first = index;
                count++;
            }
            if (count == 0 || count < bestCount || count == bestCount && selected != null && data.Id >= selected.Id) continue;
            selected = data;
            faceIndex = first;
            bestCount = count;
        }
        return selected;
    }

    private static bool Matches(SOFaceResult face, int digit, FaceKind kind)
    {
        if (kind == FaceKind.Normal)
        {
            var numbered = face.TryCast<SOFaceResultNumbered>();
            return numbered != null && numbered.digitResult == digit;
        }
        var cell = face.TryCast<SOFaceResultCellEffect>();
        return cell != null && cell.digitResult == digit && cell.IsBlessing == (kind == FaceKind.Blessing);
    }

    internal bool PrepareRoll(DiceTool die, PlayerEntity owner, ref int faceIndex)
    {
        if (!Ready || _die == null || _die.Pointer != die.Pointer || _liveChoice == null || !die.HitBoard
            || die.DiceData == null || die.DiceData.Id != _dataId || !die.SrvCanExecuteDicePostThrowEffect(owner)) return false;
        var sync = die.NetworkTransformBase;
        if (sync == null)
        { Report(Pick("测试骰子缺少位置同步组件，本次未固定骰面。", "Test die has no transform sync; this roll's face was not forced."), true); return false; }
        var info = die.DiceData.diceRotationInfos[_faceIndex];
        var rotation = Quaternion.Euler(info.eulerAngles);
        // Keep the visible upper face consistent with the server's chosen native face.
        var body = die.Rb;
        if (body != null)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.rotation = rotation;
        }
        sync.ServerTeleport(die.transform.position, rotation);
        faceIndex = _faceIndex;
        _plugin.Log.LogInfo($"[Debug dice] resolving: dice={die.netId}, faceIndex={faceIndex}, number={_liveChoice.Digit}, kind={_liveChoice.Kind}, forceDisguise={_liveChoice.Disguise}.");
        return true;
    }

    internal void SelectCurse(ref bool found, ref EAlterationType type)
    {
        if (!Ready || _liveChoice?.Disguise != true || !DisguiseDiceScopePatch.Active) return;
        if (_plugin.Disguises?.CanRoll() != true)
        { Report(Pick("玩家条件已变化，本次保留随机诅咒，未强制伪装。", "Player eligibility changed; this roll keeps its random curse instead of forcing Disguise."), true); return; }
        type = DisguiseCurse.DiceMarker;
        found = true;
        _plugin.Log.LogInfo($"[Debug dice] forced Disguise selection: dice={_die!.netId}, number={_liveChoice.Digit}; existing apply/turn flow unchanged.");
    }

    internal void Clear()
    {
        _pending = null;
        var die = _die;
        _die = null;
        _liveChoice = null;
        _connection = _board = IntPtr.Zero;
        if (NetworkServer.active && die != null) NetworkServer.Destroy(die.gameObject);
    }

    private void Report(string message, bool warning = false)
    { _status = message; _plugin.DebugTools.Report(message, warning); }
    private static string KindName(FaceKind kind) => kind switch
    { FaceKind.Normal => Pick("普通", "Normal"), FaceKind.Blessing => Pick("祝福", "Blessed"), _ => Pick("诅咒", "Cursed") };
    private static string Pick(string chinese, string english) => Lobby.LobbyBoxText.Pick(chinese, english);
    public void Dispose() => Clear();
}

[HarmonyPatch(typeof(DiceTool), nameof(DiceTool.SrvExecuteDicePostThrowEffect))]
internal static class DebugDiceRollPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(DiceTool __instance, PlayerEntity owner, ref int faceResultIndex, out DebugDiceTools? __state)
    {
        __state = DebugDiceTools.Executing;
        DebugDiceTools.Executing = null;
        var tools = CalypsoExpansionSetPlugin.Instance?.DebugTools?.Dice;
        if (tools?.PrepareRoll(__instance, owner, ref faceResultIndex) == true) DebugDiceTools.Executing = tools;
    }

    private static Exception? Finalizer(Exception? __exception, DebugDiceTools? __state)
    { DebugDiceTools.Executing = __state; return __exception; }
}

[HarmonyPatch(typeof(CurseManager), nameof(CurseManager.TryGetRandomAlterationType))]
internal static class DebugDiceCurseSelectionPatch
{
    private static void Postfix(ref bool __result, ref EAlterationType alterationType)
        => DebugDiceTools.Executing?.SelectCurse(ref __result, ref alterationType);
}
