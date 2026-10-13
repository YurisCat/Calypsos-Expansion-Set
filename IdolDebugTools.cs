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
    internal bool Enabled => _enabled.Value;
    internal bool PanelOpen { get; private set; }

    internal IdolDebugTools(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _enabled = plugin.Config.Bind("Debug", "DebugMode", false,
            "开启 MOD 测试窗口。按 WindowKey 打开，在输入框输入 spawn jumping 生成一只；list 查看类型。关闭不影响雕像效果。");
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
        if (pending.Action == DebugAction.Teleport) _plugin.Teleports.TryTeleportNext();
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
                Report("list: types | status: registry / network\nspawn <key>: spawn near you\nteleport: next MOD idol\nvisual <key> texture|white|flat: local note comparison");
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
            case "teleport" when words.Length == 1:
                QueueTeleport();
                break;
            case "visual" when words.Length == 3:
                if (!Enabled || !_plugin.Registry.Ready || !_plugin.Registry.TryGet(words[1], out var visualEntry)
                    || visualEntry.Visuals is not Visuals.IVisualDiagnostics diagnostics)
                    Report("No ready visual diagnostics for that key; use list.", true);
                else Report(diagnostics.SetDiagnosticMode(words[2].ToLowerInvariant()));
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

    private void QueueTeleport()
    {
        if (!Enabled || _plugin.RuntimeFailed) { Report("Debug teleport is disabled.", true); return; }
        var player = EntityManager.Me;
        var connection = NetworkClient.connection;
        if (!NetworkServer.active || player == null || connection == null)
        { Report("Only the host with a spawned local player can teleport to MOD idols.", true); return; }
        _pending = new DebugRequest(DebugAction.Teleport, "", player.Pointer, connection.Pointer, Time.frameCount, Time.realtimeSinceStartup + 2f);
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
        var width = Mathf.Min(640f, Screen.width - 40f);
        var x = Mathf.Max(20f, (Screen.width - width) * 0.5f);
        const float y = 40f;
        GUI.Box(new Rect(x, y, width, 380), $"Calypso's Expansion Set - Test console ({_windowKey} / Esc to close)");
        GUI.Label(new Rect(x + 15, y + 30, width - 30, 25), "Enter command here: spawn jumping   |   list   |   status");
        // Named-control APIs use span bindings that are broken in this game's interop.
        _command = GUI.TextField(new Rect(x + 15, y + 60, width - 115, 30), _command, 128);
        var execute = GUI.Button(new Rect(x + width - 90, y + 60, 75, 30), "Run");
        var guiEvent = Event.current;
        if (guiEvent.type == EventType.KeyDown && (guiEvent.keyCode == KeyCode.Return || guiEvent.keyCode == KeyCode.KeypadEnter))
        { guiEvent.Use(); execute = true; }
        if (execute) Execute();
        var entries = _plugin.Registry.Entries;
        for (var index = 0; index < Math.Min(entries.Count, 3); index++)
            if (GUI.Button(new Rect(x + 15 + index * ((width - 30) / 3), y + 105, (width - 40) / 3, 30),
                    $"Spawn {entries[index].Definition.Key}"))
                QueueSpawn(entries[index].Definition.Key);
        if (GUI.Button(new Rect(x + 15, y + 145, width - 30, 30), "Teleport to next MOD idol")) QueueTeleport();
        _resultStyle ??= new GUIStyle { wordWrap = true, fontSize = 14 };
        _resultStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 15, y + 185, width - 30, 125), _result, _resultStyle);
        var count = NetworkServer.active ? $"Debug instances: {_plugin.Sync.ServerDebugCount}" : "Spawning is host only";
        GUI.Label(new Rect(x + 15, y + 340, width - 30, 25), $"{count}. Max one test idol per type; natural idols excluded.");
    }

    internal void Cancel() { _pending = null; Close(); _plugin.Teleports.Reset(); }
    public void Dispose() => Cancel();
}
