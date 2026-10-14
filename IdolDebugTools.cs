using BepInEx.Configuration;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using User;
using _200_Dev.Entities;

namespace CalypsoExpansionSet;

internal sealed class IdolDebugTools : IDisposable
{
    private enum DebugAction { Spawn, Teleport }
    private sealed record DebugRequest(DebugAction Action, string Key, IntPtr Player, IntPtr Connection, int Frame, float Expires);
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly ConfigEntry<bool> _enabled;
    private readonly Key _windowKey;
    private InputActionMap? _disabledMap;
    private bool _mapWasEnabled;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private DebugRequest? _pending;
    private string _command = "spawn jumping";
    private string _result = "Commands: list | status | spawn <key> | teleport\nExample: spawn jumping";
    private GUIStyle? _resultStyle;
    private Rect _windowRect;
    private bool _windowPositioned;
    private int _dragControl;
    private Vector2 _idolScroll;
    private string[] _spawnLabels = Array.Empty<string>();
    internal bool Enabled => _enabled.Value;
    internal bool PanelOpen { get; private set; }

    internal IdolDebugTools(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _enabled = plugin.Config.Bind("Debug", "DebugMode", false,
            "开启 MOD 调试模式，启用测试工具和诊断日志；关闭不影响正常玩法。");
        var key = plugin.Config.Bind("Debug", "WindowKey", "F8",
            "打开/关闭测试窗口的唯一按键，默认 F8；窗口内输入命令或点击生成按钮。None 禁用按键。");
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
            Report("Debug action cancelled: player/session changed or controls did not resume. Close other menus and retry.", true);
            return;
        }
        if (!_plugin.LocalGameplayReady(player)) return;
        _pending = null;
        if (pending.Action == DebugAction.Teleport) _plugin.Teleports.TryTeleportNext(string.IsNullOrEmpty(pending.Key) ? null : pending.Key);
        else if (_plugin.SpawnNearPlayer(pending.Key))
            Report($"Spawned {pending.Key} near you. Open {_windowKey} again for another command.");
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
                Report("list: types | status: registry / network\nspawn <key>: spawn near you\nteleport [key]: next matching MOD idol\nvisual <key> texture|white|flat: local note comparison");
                break;
            case "list" when words.Length == 1:
                Report("Available types: " + string.Join(", ", _plugin.Registry.Entries.Select(entry => entry.Definition.Key)));
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
        if (!_plugin.Registry.Ready || !_plugin.Registry.TryGet(key, out _))
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

    internal void Report(string message, bool warning = false)
    {
        _result = message;
        if (warning) _plugin.Log.LogWarning(message); else _plugin.Log.LogInfo(message);
    }

    internal void Draw()
    {
        if (!Enabled || !PanelOpen) return;
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
        var listHeight = Mathf.Max(1f, _windowRect.height - 340f);
        DrawSpawnButtons(new Rect(x + 15, y + 125, width - 30, listHeight));
        var actionsY = y + 135 + listHeight;
        if (GUI.Button(new Rect(x + 15, actionsY, width - 30, 30), "Teleport to next MOD idol")) QueueTeleport();
        _resultStyle ??= new GUIStyle { wordWrap = true, fontSize = 14 };
        _resultStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 15, actionsY + 40, width - 30, 125), _result, _resultStyle);
        var count = NetworkServer.active ? $"Debug instances: {_plugin.Sync.ServerDebugCount}" : "Spawning is host only";
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

    internal void Cancel() { _pending = null; Close(); _plugin.Teleports.Reset(); Testing.TestRoomAppearanceView.Stop(); }
    public void Dispose() => Cancel();
}
