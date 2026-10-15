using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyBoxEditor
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly string[] _fields = new string[10];
    private bool _initialized;
    private IntPtr _modelPointer;
    private string _status = "";
    private bool _numeric;
    private int _dragAxis = -1, _dragControl;
    private float _pressX, _lastTick, _dragValue;
    private Vector2 _scroll;
    internal LobbyBoxEditor(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;
    private static string T(string zh, string en) => LobbyBoxText.Pick(zh, en);

    internal void Draw(Rect area)
    {
        var box = _plugin.LobbyBox;
        if (box == null || !box.Ready)
        {
            _initialized = false; CancelDrag();
            GUI.Label(area, T("进入大厅并等待模型加载。", "Enter the lobby and wait for the model."));
            return;
        }
        if (!_initialized || _modelPointer != box.ModelTransform!.Pointer)
        { CancelDrag(); Refresh(); _modelPointer = box.ModelTransform!.Pointer; }
        var width = Mathf.Max(220, area.width - 20);
        _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0, 0, width, 615), false, true);
        try
        {
            GUI.Label(new Rect(0, 0, width, 25), T("模型：桌游盒子（expansion_box）", "Model: board-game box (expansion_box)"));
            var third = (width - 12) / 3;
            if (GUI.Button(new Rect(0, 30, third, 26), T("移到面前", "Bring here")))
                Run(() => { CancelDrag(); box.BringToPlayer(); Refresh(); });
            if (GUI.Button(new Rect(third + 6, 30, third, 26), T("恢复默认", "Reset")))
                Run(() => { CancelDrag(); box.Edit(new LobbyBoxPlacement()); Refresh(); });
            if (GUI.Button(new Rect(2 * (third + 6), 30, third, 26), _numeric ? T("切回拖动", "Drag mode") : T("数值输入", "Type values")))
            { CancelDrag(); _numeric = !_numeric; }
            GUI.Label(new Rect(0, 62, width, 24),
                _numeric ? T("输入后应用 / 位置（米）、旋转（度）、缩放（倍）", "Apply after editing / position (m), rotation (degrees), scale")
                    : T("按住数值左右拖；离按下点越远越快，回到原点停止", "Hold a value and drag sideways; farther = faster, return = stop"));
            var half = (width - 60) / 3;
            for (var group = 0; group < 3; group++)
            {
                var y = 92 + group * 64;
                var name = group == 0 ? T("位置", "Position") : group == 1 ? T("旋转", "Rotation") : T("缩放", "Scale");
                GUI.Label(new Rect(0, y, width, 22), name);
                for (var axis = 0; axis < 3; axis++)
                {
                    var index = group * 3 + axis;
                    var x = axis * (half + 20);
                    GUI.Label(new Rect(x, y + 25, 17, 24), "XYZ"[axis].ToString());
                    var field = new Rect(x + 20, y + 25, half - 2, 26);
                    if (_numeric) _fields[index] = GUI.TextField(field, _fields[index], 16);
                    else DragField(field, index);
                }
            }
            if (GUI.Button(new Rect(0, 291, third, 28), T("应用数值", "Apply values"))) Run(ApplyFields);
            if (GUI.Button(new Rect(third + 6, 291, third, 28), T("保存摆放", "Save placement")))
                Run(() => { ApplyFields(); box.SavePlacement(); _status = T("已保存：", "Saved: ") + box.PlacementPath; });
            if (GUI.Button(new Rect(2 * (third + 6), 291, third, 28), T("读取摆放", "Load placement")))
                Run(() => { box.LoadPlacement(); Refresh(); _status = T("已读取保存的摆放。", "Saved placement loaded."); });
            GUI.Label(new Rect(0, 330, width, 24), T("盒子补光（随大厅停电／闪烁）", "Box fill lighting (follows room power / flicker)"));
            var lightWidth = (width - 30) / 6;
            for (var preset = 0; preset <= 5; preset++)
            {
                var current = box.Lighting?.Preset == preset;
                if (GUI.Button(new Rect(preset * (lightWidth + 6), 358, lightWidth, 26),
                        (current ? "● " : "") + (preset == 0 ? T("关", "Off") : preset.ToString())))
                {
                    var selected = preset;
                    Run(() => { var data = box.Capture(); data.LightPreset = selected; box.Edit(data); });
                }
            }
            GUI.Label(new Rect(0, 389, width, 24), LobbyBoxLighting.Name(box.Lighting?.Preset ?? 0)
                + (box.Lighting?.HasReference == true ? "" : T("（等待大厅灯具）", " (waiting for room fixture)")));
            GUI.Label(new Rect(0, 422, 180, 26), T("灯光亮度（0～5 倍）", "Brightness (0–5×)"));
            var brightnessRect = new Rect(185, 420, width - 185, 28);
            if (_numeric) _fields[9] = GUI.TextField(brightnessRect, _fields[9], 16);
            else DragField(brightnessRect, 9);
            GUI.enabled = Testing.TestRoomLaunch.IsActive;
            if (GUI.Button(new Rect(0, 475, width, 28),
                    T("测试大厅满配：", "Fully furnished test lobby: ") + (box.Furniture.Requested ? T("开", "ON") : T("关", "OFF"))))
                box.Furniture.Requested = !box.Furniture.Requested;
            GUI.enabled = true;
            GUI.Label(new Rect(0, 511, width, 40), T(
                "保存时记录光照编号与亮度；满配仅限专用测试房。",
                "Saving includes the lighting preset and brightness; full furnishing is test-room only."));
            var style = GUI.skin.label;
            var previousWrap = style.wordWrap;
            try { style.wordWrap = true; GUI.Label(new Rect(0, 563, width, 65), _status, style); }
            finally { style.wordWrap = previousWrap; }
        }
        finally { GUI.enabled = true; GUI.EndScrollView(); }
    }

    private void Refresh()
    {
        var data = _plugin.LobbyBox!.Capture();
        var values = data.Position.Concat(data.Rotation).Concat(data.Scale).ToArray();
        for (var i = 0; i < values.Length; i++) _fields[i] = values[i].ToString("0.####", CultureInfo.InvariantCulture);
        _fields[9] = data.LightBrightness.ToString("0.####", CultureInfo.InvariantCulture);
        _initialized = true;
    }
    private void ApplyFields()
    {
        var values = _fields.Select(text => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        var data = _plugin.LobbyBox!.Capture();
        data.Position = values.Take(3).ToArray(); data.Rotation = values.Skip(3).Take(3).ToArray(); data.Scale = values.Skip(6).Take(3).ToArray();
        data.LightBrightness = values[9];
        _plugin.LobbyBox.Edit(data);
        Refresh();
    }
    private void DragField(Rect rect, int index)
    {
        var control = GUIUtility.GetControlID(0x43455370 + index, FocusType.Passive);
        GUI.Box(rect, (_dragAxis == index ? "↔ " : "") + _fields[index]);
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && GUIUtility.hotControl == 0 && rect.Contains(evt.mousePosition))
        {
            _pressX = Mouse.current?.position.ReadValue().x ?? GUIUtility.GUIToScreenPoint(evt.mousePosition).x;
            _dragAxis = index; _dragValue = float.Parse(_fields[index], CultureInfo.InvariantCulture);
            _dragControl = GUIUtility.hotControl = control; _lastTick = Time.realtimeSinceStartup;
            evt.Use();
        }
        else if (_dragControl == control && GUIUtility.hotControl == control)
        {
            if (evt.type == EventType.MouseUp && evt.button == 0) { CancelDrag(); evt.Use(); }
            else if (evt.type == EventType.MouseDrag) evt.Use();
        }
    }

    internal void Tick()
    {
        if (_dragAxis < 0) return;
        var mouse = Mouse.current;
        var box = _plugin.LobbyBox;
        if (!_plugin.DebugTools.Enabled || !_plugin.DebugTools.PanelOpen || !Application.isFocused
            || mouse == null || !mouse.leftButton.isPressed || box?.ModelTransform == null
            || box.ModelTransform.Pointer != _modelPointer || GUIUtility.hotControl != _dragControl)
        { CancelDrag(); return; }
        var now = Time.realtimeSinceStartup;
        var deltaTime = Mathf.Clamp(now - _lastTick, 0, 0.05f); _lastTick = now;
        var group = _dragAxis / 3;
        var velocity = LobbyBoxTuning.DragVelocity(mouse.position.ReadValue().x - _pressX, group);
        if (velocity == 0) return;
        try
        {
            _dragValue += velocity * deltaTime;
            _dragValue = group == 0 ? Mathf.Clamp(_dragValue, -200, 200)
                : group == 1 ? Mathf.Repeat(_dragValue, 360) : group == 2 ? Mathf.Clamp(_dragValue, 0.01f, 20)
                : Mathf.Clamp(_dragValue, 0, 5);
            var data = box.Capture();
            if (group == 3) data.LightBrightness = _dragValue;
            else
            {
                var target = group == 0 ? data.Position : group == 1 ? data.Rotation : data.Scale;
                target[_dragAxis % 3] = _dragValue;
            }
            box.Edit(data);
            Refresh();
        }
        catch (Exception error) { CancelDrag(); _status = error.Message; _plugin.Log.LogWarning("[Lobby drag] " + error.Message); }
    }

    internal void CancelDrag()
    {
        if (_dragControl != 0 && GUIUtility.hotControl == _dragControl) GUIUtility.hotControl = 0;
        _dragControl = 0; _dragAxis = -1;
    }
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            _status = T("操作未完成：", "Action failed: ") + error.Message;
            _plugin.Log.LogWarning("[Lobby editor] " + error.Message);
        }
    }
}
