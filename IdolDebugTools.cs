using BepInEx.Configuration;
using Entities.Player;
using EnvironmentElements;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using User;
using _200_Dev.BoardGameLogic;
using _200_Dev.Entities;
using _200_Dev.FlowLogic;

namespace CalypsoExpansionSet;

internal sealed class IdolDebugTools : IDisposable
{
    private enum DebugAction { Spawn, Teleport, NextRound, AdvancePawn, ReviveAll, ToggleInvulnerability, TeleportAllHome, ToggleYeti, ToggleHunter }
    private sealed record DebugRequest(DebugAction Action, string Key, IntPtr Player, IntPtr Connection, int Frame, float Expires,
        IntPtr Board = default);
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly ConfigEntry<bool> _enabled;
    private readonly Key _windowKey;
    private readonly IdolDebugMonsters _monsters;
    internal IdolDebugCatalog Catalog { get; }
    private InputActionMap? _disabledMap;
    private bool _mapWasEnabled;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private DebugRequest? _pending;
    private string _command = "spawn jumping";
    private string _result = "Commands: list | status | spawn <key> | teleport | next | revive\nExample: spawn jumping";
    private float _nextRoundAfter;
    private PlayerEntity? _invulnerablePlayer;
    private IntPtr _invulnerableConnection;
    private bool _previousInvulnerability;
    private GUIStyle? _resultStyle;
    private Rect _windowRect;
    private bool _windowPositioned;
    private int _dragControl;
    private Vector2 _idolScroll;
    private Vector2 _resultScroll;
    private string[] _spawnLabels = Array.Empty<string>();
    internal bool Enabled => _enabled.Value;
    internal bool PanelOpen { get; private set; }

    internal IdolDebugTools(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        Catalog = new IdolDebugCatalog(plugin);
        _monsters = new IdolDebugMonsters(plugin);
        _enabled = plugin.Config.Bind("Debug", "DebugMode", false,
            "启用测试工具和诊断日志，关闭不影响正常玩法。修改后重启。\n"
            + "/ Enable test tools and diagnostic logs; normal gameplay works when off. Restart after changes.");
        var key = plugin.Config.Bind("Debug", "WindowKey", "F8",
            "调试窗口开关键，需 DebugMode=true；None 禁用快捷键。修改后重启。\n"
            + "/ Toggle the test window (requires DebugMode=true); None disables the shortcut. Restart after changes.");
        if (!Enum.TryParse(key.Value.Trim(), true, out _windowKey) || !Enum.IsDefined(typeof(Key), _windowKey))
        {
            _windowKey = Key.F8;
            key.Value = "F8";
            plugin.Log.LogWarning("Invalid Debug.WindowKey; using F8.");
        }
        // Bind consumes the old orphaned entry before Remove, so Save also drops old keys.
        RemoveLegacy("SpawnKey", Key.F8);
        RemoveLegacy("PanelKey", Key.F9);
        RemoveLegacy("ConsoleKey", Key.F10);
        RemoveLegacy("TestIdol", "jumping");
        RemoveLegacy("MaxTestIdols", 16);
        plugin.Config.Save();
    }

    private void RemoveLegacy<T>(string name, T fallback)
    {
        var old = _plugin.Config.Bind("Debug", name, fallback, "Obsolete setting.");
        _plugin.Config.Remove(old.Definition);
    }

    internal void Tick()
    {
        if (!Enabled) { Cancel(); return; }
        _monsters.Tick();
        if (_invulnerablePlayer is { } protectedPlayer
            && (protectedPlayer == null || !NetworkServer.active || !NetworkClient.ready
                || EntityManager.Me == null || EntityManager.Me.Pointer != protectedPlayer.Pointer
                || NetworkClient.connection == null || NetworkClient.connection.Pointer != _invulnerableConnection
                || protectedPlayer.CurrentHealthSync <= 0 || !protectedPlayer.IsInvulnerable))
            RestoreInvulnerability();
        var keyboard = Keyboard.current;
        if (keyboard != null && _windowKey != Key.None && keyboard[_windowKey].wasPressedThisFrame)
        {
            if (PanelOpen) Close(); else Open();
        }
        if (PanelOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        if (PanelOpen && _disabledMap != null && _mapWasEnabled && _disabledMap.enabled)
            _disabledMap.Disable();
        if (_pending is not { } pending || Time.frameCount <= pending.Frame) return;
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!NetworkServer.active || player == null || player.Pointer != pending.Player
            || connection == null || connection.Pointer != pending.Connection || Time.realtimeSinceStartup > pending.Expires)
        {
            _pending = null;
            Report("Debug action cancelled: player/session changed or action timed out. Wait for transitions, close other menus and retry.", true);
            return;
        }
        var sessionAction = pending.Action is DebugAction.NextRound or DebugAction.AdvancePawn or DebugAction.ReviveAll
            or DebugAction.ToggleInvulnerability or DebugAction.TeleportAllHome or DebugAction.ToggleYeti or DebugAction.ToggleHunter;
        // Session tools must also work while the host is dead or seated at the board.
        if (sessionAction ? PanelOpen || Time.timeScale <= 0f : !_plugin.LocalGameplayReady(player)) return;
        _pending = null;
        if (sessionAction)
        {
            try
            {
                if (!NetworkClient.ready || _plugin.RuntimeFailed)
                    Report("Debug action unavailable: session is not ready or the plugin failed.", true);
                else if (pending.Action == DebugAction.ReviveAll) ReviveAll();
                else if (pending.Action == DebugAction.ToggleInvulnerability) ToggleInvulnerability();
                else if (pending.Action == DebugAction.TeleportAllHome) TeleportAllHome();
                else if (pending.Action is DebugAction.ToggleYeti or DebugAction.ToggleHunter)
                    _monsters.Toggle(pending.Action == DebugAction.ToggleHunter);
                else NextRound(pending);
            }
            catch (Exception exception)
            {
                Report("Debug action failed; check the log before retrying.", true);
                _plugin.Log.LogError(exception);
            }
            return;
        }
        try
        {
            if (pending.Action == DebugAction.Teleport) _plugin.Teleports.TryTeleportNext(string.IsNullOrEmpty(pending.Key) ? null : pending.Key);
            else if (_plugin.SpawnNearPlayer(pending.Key))
                Report($"Spawned {pending.Key} near you. Open {_windowKey} again for another command.");
        }
        catch (Exception exception)
        {
            Report("Debug action failed; check the log before retrying.", true);
            _plugin.Log.LogError(exception);
        }
    }

    private void Open()
    {
        PanelOpen = true;
        var inputs = InputsManager.Instance;
        _disabledMap = inputs != null ? inputs.currentActionMap : null;
        _mapWasEnabled = _disabledMap != null && _disabledMap.enabled;
        if (_mapWasEnabled) _disabledMap!.Disable();
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        if (!PanelOpen) return;
        if (_dragControl != 0 && GUIUtility.hotControl == _dragControl) GUIUtility.hotControl = 0;
        _dragControl = 0;
        PanelOpen = false;
        var inputs = InputsManager.Instance;
        var currentMap = inputs != null ? inputs.currentActionMap : null;
        var sameMap = _disabledMap == null || currentMap != null && currentMap.Pointer == _disabledMap.Pointer;
        if (sameMap)
        {
            if (_mapWasEnabled && _disabledMap != null && !_disabledMap.enabled) _disabledMap.Enable();
            if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
            }
        }
        _disabledMap = null;
        _mapWasEnabled = false;
    }

    private void Execute()
    {
        var words = _command.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return;
        switch (words[0].ToLowerInvariant())
        {
            case "help" when words.Length == 1:
                Report("list: MOD + original spawn keys | status: registry / network\nspawn <key>: spawn near you (e.g. miniature; use original: for native names shared with MODs)\nteleport [key]: next matching MOD idol\nnext: advance round with a random 1-6 roll (host)\nrevive: revive dead players and fully heal all players (host)\nYeti / Hunter buttons: spawn or remove your manual test monster (host)\nvisual <key> texture|white|flat: local note comparison");
                break;
            case "list" when words.Length == 1:
                var originals = Catalog.GetOriginals();
                Report("MOD spawn keys:\n" + string.Join("\n", _plugin.Registry.Entries.Select(entry => entry.Definition.Key))
                    + "\nOriginal spawn keys:\n" + (originals.Count == 0 ? "Not ready; retry after loading."
                        : string.Join("\n", originals.Select(entry =>
                        {
                            var key = entry.Key[9..];
                            if (_plugin.Registry.TryGet(key, out _)) key = entry.Key;
                            return $"{key} ({entry.Data.name})";
                        }))));
                break;
            case "status" when words.Length == 1:
                Report(_plugin.Status);
                break;
            case "spawn" when words.Length == 2:
                QueueSpawn(words[1].ToLowerInvariant());
                break;
            case "teleport" when words.Length is 1 or 2:
                QueueTeleport(words.Length == 2 ? words[1].ToLowerInvariant() : "");
                break;
            case "next" when words.Length == 1:
                QueueSessionAction(DebugAction.NextRound);
                break;
            case "revive" when words.Length == 1:
                QueueSessionAction(DebugAction.ReviveAll);
                break;
            case "visual" when words.Length == 3:
                if (!Enabled || !_plugin.Registry.Ready || !_plugin.Registry.TryGet(words[1], out var visualEntry)
                    || visualEntry.Visuals is not Visuals.IVisualDiagnostics diagnostics)
                    Report("No ready visual diagnostics for that key; use list.", true);
                else Report(diagnostics.SetDiagnosticMode(words[2].ToLowerInvariant()));
                break;
            case "inspect" when words.Length == 1:
                if (Testing.TestRoomAppearanceView.Toggle()) Close();
                else Report("inspect is only available in the explicit single-player test room.", true);
                break;
            case "hintcheck" when words.Length == 1:
                if (_plugin.HoverInfo?.TestMessagePriority() == true)
                { Report("Native message shown for 20s; point at a MOD idol to check the separate hint line."); Close(); }
                else Report("hintcheck requires the explicit single-player test room and an idle native message area.", true);
                break;
            default:
                Report("Unknown command. Use help for commands.", true);
                break;
        }
    }

    private void QueueSpawn(string key)
    {
        if (!Enabled) { Report("DebugMode is disabled.", true); return; }
        if (_plugin.RuntimeFailed) { Report("Runtime disabled after an error; check the log and restart.", true); return; }
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!NetworkServer.active || player == null || connection == null)
        { Report("Only the host with a spawned local player can spawn test idols.", true); return; }
        if (!_plugin.Registry.Ready || (!_plugin.Registry.TryGet(key, out _) && !Catalog.TryGet(key, out _)))
        { Report($"Unknown or unready type '{key}'. Use list to see registered types.", true); return; }
        _pending = new DebugRequest(DebugAction.Spawn, key, player.Pointer, connection.Pointer, Time.frameCount, Time.realtimeSinceStartup + 2f);
        Close();
    }

    private void QueueTeleport(string key = "")
    {
        if (!Enabled || _plugin.RuntimeFailed) { Report("Debug teleport is disabled.", true); return; }
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!NetworkServer.active || player == null || connection == null)
        { Report("Only the host with a spawned local player can teleport to MOD idols.", true); return; }
        if (key.Length != 0 && !_plugin.Registry.TryGet(key, out _))
        { Report($"Unknown type '{key}'. Use list to see registered types.", true); return; }
        _pending = new DebugRequest(DebugAction.Teleport, key, player.Pointer, connection.Pointer, Time.frameCount, Time.realtimeSinceStartup + 2f);
        Close();
    }

    private void QueueSessionAction(DebugAction action)
    {
        if (!Enabled || _plugin.RuntimeFailed) { Report("Debug session tools are disabled.", true); return; }
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!NetworkServer.active || !NetworkClient.ready || player == null || connection == null)
        { Report("Only the host in a ready game session can use these debug session tools.", true); return; }
        if (_pending != null) { Report("A debug action is already pending; wait for it to finish.", true); return; }
        var board = BoardGameManager.Instance;
        _pending = new DebugRequest(action, "", player.Pointer, connection.Pointer, Time.frameCount,
            Time.realtimeSinceStartup + 10f, board != null ? board.Pointer : IntPtr.Zero);
        Close();
    }

    private void NextRound(DebugRequest request)
    {
        var board = BoardGameManager.Instance;
        var events = GameEventManager.Instance;
        var zone = ZoneManager.Instance;
        var rules = board != null ? board.BoardGameRulesEngine : null;
        if (Testing.TestRoomLaunch.IsActive)
        { Report("next is unavailable in the gallery: native game events are disabled there.", true); return; }
        if (board == null || board.BoardGameTool == null || board.BoardGameTool.SnapPoint == null)
        { Report("Place the board on the table before using next.", true); return; }
        if (board == null || board.Pointer != request.Board || !board.SrvIsActive || rules == null
            || board.PawnManager == null || events == null || events.CurrentGameEventData == null || zone == null
            || rules.AllAgentsHaveFinished() || !_plugin.Sync.AllReadyPeersCompatible)
        { Report("next requires an active board and compatible ready players; finish loading / start a board first.", true); return; }
        // SrvAllPawnMoveCompleted also fires gameplay callbacks; only read pending acknowledgements.
        var busy = (Flow.Instance != null && Flow.IsPlaying)
            || events._isCurrentEventLocked || zone._shopSpawnPending || zone._clientsNotReady?.Count > 0
            || board.PawnManager._currentPawnMoveCompleted?.Count > 0 || board.BoardToolSequencer?._srvCheatToolsQueue?.Count > 0;
        if (request.Action == DebugAction.NextRound)
        {
            if (busy || Time.realtimeSinceStartup < _nextRoundAfter)
            { Report("Board / room transition or dice action in progress; wait and retry next.", true); return; }
            if (events.CurrentGameEventDataId != events.NoGameEventId)
                events.CurrentGameEventData.SrvGameEventCompleted();
            // Native event completion may finish asynchronously. Move once it is idle.
            _pending = request with { Action = DebugAction.AdvancePawn, Frame = Time.frameCount };
            return;
        }
        if (busy || events.CurrentGameEventDataId != events.NoGameEventId || board.BoardGameState != EBoardGameState.Playable)
        { _pending = request; return; }
        var agent = rules.GetAgent(rules.currentTurnAgentId);
        if (agent == null || !rules.CanAgentPlay(agent))
        { Report("No playable current turn; wait for the board or revive dead players first.", true); return; }
        var roll = UnityEngine.Random.Range(1, 7);
        rules.SrvMovePawn(roll);
        _nextRoundAfter = Time.realtimeSinceStartup + 2f;
        Report($"Advanced the current pawn with debug roll {roll}; native events / room generation continue.");
    }

    private void ReviveAll()
    {
        var revived = 0;
        var healed = 0;
        var skipped = 0;
        var users = UserInstancesManager.Instance;
        if (users == null) { Report("Player list is not ready; retry after loading.", true); return; }
        foreach (var user in users.All())
        {
            var player = user != null ? user.playerEntity : null;
            if (player == null || player.EntityStatesRunner == null) continue;
            var state = player.EntityStatesRunner.GetLifeCycleEsm()?.CurrentStateType;
            if (state == null) continue;
            var dead = state == EntityManager.LifeCycleEntityStateType.Dead;
            if (!dead && player.CurrentHealthSync <= 0) continue;
            var connection = player.PlayerMovement != null ? player.PlayerMovement.connectionToClient : null;
            if (connection == null || !connection.isAuthenticated || !connection.isReady || player.MaxHealthSync <= 0)
            { skipped++; continue; }
            if (dead)
            {
                if (!ZoneManager.SrvTryGetPlayerSpawnPosition(player, out var position, out _))
                { skipped++; continue; }
                player.SrvResurrect(new Il2CppSystem.Nullable<Vector3>(position), new Il2CppSystem.Nullable<int>(player.MaxHealthSync));
                if (player.CurrentHealthSync <= 0) { skipped++; continue; }
                revived++;
            }
            if (player.CurrentHealthSync < player.MaxHealthSync)
            {
                player.SrvSetCurrentHealth(player.MaxHealthSync);
                if (!dead) healed++;
            }
        }
        Report($"Revived {revived} dead player(s); fully healed {healed} living player(s). Skipped {skipped} unready player(s).", skipped > 0);
    }

    private void ToggleInvulnerability()
    {
        if (_invulnerablePlayer is not null)
        {
            RestoreInvulnerability();
            Report("Host damage protection OFF.");
            return;
        }
        var player = EntityManager.Me;
        if (player == null || player.CurrentHealthSync <= 0)
        { Report("Revive the host before enabling damage protection.", true); return; }
        _previousInvulnerability = player.IsInvulnerable;
        _invulnerablePlayer = player;
        _invulnerableConnection = NetworkClient.connection!.Pointer;
        player.IsInvulnerable = true;
        Report("Host damage protection ON (native damage immunity; other players unchanged).");
    }

    private void RestoreInvulnerability()
    {
        var player = _invulnerablePlayer;
        _invulnerablePlayer = null;
        _invulnerableConnection = IntPtr.Zero;
        // Preserve pre-existing immunity and changes made after our last write.
        if (player != null && player.IsInvulnerable) player.IsInvulnerable = _previousInvulnerability;
    }

    private void TeleportAllHome()
    {
        var users = UserInstancesManager.Instance;
        if (users == null) { Report("Player list is not ready; retry after loading.", true); return; }
        var teleported = 0;
        var skipped = 0;
        foreach (var user in users.All())
        {
            var player = user != null ? user.playerEntity : null;
            if (player == null || player.CurrentHealthSync <= 0 || player.EntityStatesRunner == null) continue;
            var state = player.EntityStatesRunner.GetLifeCycleEsm()?.CurrentStateType;
            if (state == null || state == EntityManager.LifeCycleEntityStateType.Dead
                || state == EntityManager.LifeCycleEntityStateType.Dying) continue;
            var movement = player.PlayerMovement;
            var connection = movement != null ? movement.connectionToClient : null;
            if (connection == null || !connection.isAuthenticated || !connection.isReady
                || !ZoneManager.SrvTryGetPlayerSpawnPosition(player, out var position, out var rotation))
            { skipped++; continue; }
            movement!.TargetTeleport(connection, position, rotation);
            teleported++;
        }
        Report($"Teleported {teleported} living player(s) to their native spawn points. Skipped {skipped} unready player(s).", skipped > 0);
    }

    internal void Report(string message, bool warning = false)
    {
        _result = message;
        _resultScroll = default;
        if (warning) _plugin.Log.LogWarning(message); else _plugin.Log.LogInfo(message);
    }

    internal void Draw()
    {
        if (!Enabled) return;
        if (!PanelOpen) return;
        try { DrawPanel(); }
        catch (Exception exception)
        {
            Cancel();
            _plugin.Log.LogError($"Test window closed after a UI error; game controls restored.\n{exception}");
        }
    }

    private void DrawPanel()
    {
        _windowRect.width = Mathf.Max(1f, Mathf.Min(640f, Screen.width - 20f));
        _windowRect.height = Mathf.Max(1f, Mathf.Min(520f, Screen.height - 20f));
        if (!_windowPositioned)
        {
            _windowRect.position = new Vector2((Screen.width - _windowRect.width) * 0.5f, 40f);
            _windowPositioned = true;
        }
        ClampWindowPosition();
        HandleWindowDrag();
        var x = _windowRect.x;
        var y = _windowRect.y;
        var width = _windowRect.width;
        GUI.Box(_windowRect, $"Calypso's Expansion Set - Test console ({_windowKey} / Esc to close; drag title)");
        GUI.Label(new Rect(x + 15, y + 30, width - 30, 25), "Enter command here: spawn jumping   |   list   |   status");
        // TextField consumes Return, so capture submission before drawing it.
        var guiEvent = Event.current;
        var submit = guiEvent.type == EventType.KeyDown
            && (guiEvent.keyCode == KeyCode.Return || guiEvent.keyCode == KeyCode.KeypadEnter);
        if (submit) guiEvent.Use();
        // Named-control APIs use span bindings that are broken in this game's interop.
        _command = GUI.TextField(new Rect(x + 15, y + 60, width - 115, 30), _command, 128);
        var execute = GUI.Button(new Rect(x + width - 90, y + 60, 75, 30), "Run");
        if (submit || execute) Execute();
        GUI.Label(new Rect(x + 15, y + 100, width - 30, 25), "MOD idols: click to spawn; scroll for more");
        var listHeight = Mathf.Max(1f, _windowRect.height - 374f);
        DrawSpawnButtons(new Rect(x + 15, y + 125, width - 30, listHeight));
        var actionsY = y + 135 + listHeight;
        if (GUI.Button(new Rect(x + 15, actionsY, width - 30, 30), "Teleport to next MOD idol")) QueueTeleport();
        var actionWidth = (width - 54f) * 0.25f;
        if (GUI.Button(new Rect(x + 15, actionsY + 38, actionWidth, 26), "下一关")) QueueSessionAction(DebugAction.NextRound);
        if (GUI.Button(new Rect(x + 23 + actionWidth, actionsY + 38, actionWidth, 26), "复活")) QueueSessionAction(DebugAction.ReviveAll);
        if (GUI.Button(new Rect(x + 31 + actionWidth * 2, actionsY + 38, actionWidth, 26),
                _invulnerablePlayer != null ? "无敌：开" : "无敌：关")) QueueSessionAction(DebugAction.ToggleInvulnerability);
        if (GUI.Button(new Rect(x + 39 + actionWidth * 3, actionsY + 38, actionWidth, 26), "传送")) QueueSessionAction(DebugAction.TeleportAllHome);
        var monsterWidth = (width - 38f) * 0.5f;
        if (GUI.Button(new Rect(x + 15, actionsY + 72, monsterWidth, 26), _monsters.HasYeti ? "雪人：移除" : "雪人：生成"))
            QueueSessionAction(DebugAction.ToggleYeti);
        if (GUI.Button(new Rect(x + 23 + monsterWidth, actionsY + 72, monsterWidth, 26), _monsters.HasHunter ? "猎人：移除" : "猎人：生成"))
            QueueSessionAction(DebugAction.ToggleHunter);
        _resultStyle ??= new GUIStyle { wordWrap = true, fontSize = 14 };
        _resultStyle.normal.textColor = Color.white;
        var resultWidth = Mathf.Max(1f, width - 50f);
        var resultHeight = Mathf.Max(95f, _resultStyle.CalcHeight(new GUIContent(_result), resultWidth));
        _resultScroll = GUI.BeginScrollView(new Rect(x + 15, actionsY + 112, width - 30, 90), _resultScroll,
            new Rect(0, 0, resultWidth, resultHeight), false, true);
        try { GUI.Label(new Rect(0, 0, resultWidth, resultHeight), _result, _resultStyle); }
        finally { GUI.EndScrollView(); }
        var count = NetworkServer.active ? $"Debug idols: {_plugin.Sync.ServerDebugCount + Catalog.InstanceCount}" : "Spawning is host only";
        GUI.Label(new Rect(x + 15, y + _windowRect.height - 35, width - 30, 25), $"{count}. Max one test idol per type; natural idols excluded.");
    }

    private void DrawSpawnButtons(Rect viewport)
    {
        var entries = _plugin.Registry.Entries;
        if (_spawnLabels.Length != entries.Count)
            _spawnLabels = entries.Select(entry => $"Spawn {entry.Definition.DisplayName} ({entry.Definition.Key})").ToArray();
        if (entries.Count == 0)
        {
            GUI.Label(viewport, "No MOD idols registered.");
            return;
        }
        var columns = viewport.width >= 460f ? 2 : 1;
        var contentWidth = Mathf.Max(1f, viewport.width - 20f);
        var buttonWidth = (contentWidth - (columns - 1) * 8f) / columns;
        var rows = (entries.Count + columns - 1) / columns;
        _idolScroll = GUI.BeginScrollView(viewport, _idolScroll,
            new Rect(0, 0, contentWidth, rows * 38f), false, true);
        try
        {
            for (var index = 0; index < entries.Count; index++)
                if (GUI.Button(new Rect(index % columns * (buttonWidth + 8f), index / columns * 38f, buttonWidth, 30f),
                        _spawnLabels[index]))
                    QueueSpawn(entries[index].Definition.Key);
        }
        finally { GUI.EndScrollView(); }
    }

    private void HandleWindowDrag()
    {
        var control = GUIUtility.GetControlID(0x43455344, FocusType.Passive);
        var guiEvent = Event.current;
        if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && GUIUtility.hotControl == 0
            && new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 25f).Contains(guiEvent.mousePosition))
        {
            GUIUtility.hotControl = _dragControl = control;
            guiEvent.Use();
        }
        else if (_dragControl == control && GUIUtility.hotControl == control)
        {
            if (guiEvent.type == EventType.MouseDrag)
            {
                _windowRect.position += guiEvent.delta;
                ClampWindowPosition();
                guiEvent.Use();
            }
            else if (guiEvent.type == EventType.MouseUp && guiEvent.button == 0)
            {
                GUIUtility.hotControl = _dragControl = 0;
                guiEvent.Use();
            }
        }
    }

    private void ClampWindowPosition()
    {
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
    }

    internal void Cancel() { _pending = null; Close(); _monsters.Clear(); RestoreInvulnerability(); _plugin.Teleports.Reset(); Testing.TestRoomAppearanceView.Stop(); }
    public void Dispose() => Cancel();
}
