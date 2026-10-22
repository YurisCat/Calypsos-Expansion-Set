using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

namespace CalypsoExpansionSet.UI;

// Only the draft action is rebound. The game's action asset and saved overrides are never edited.
internal sealed class ExpansionSettingsBindings : IDisposable
{
    internal string MouseKeyboard = "", Gamepad = "";
    internal bool Busy => _index >= 0;
    internal int DeviceIndex => _index;
    internal bool IsRecording(int index) => _index == index && !_finishing;
    private int _index = -1;
    private InputAction? _action;
    private RebindingOperation? _operation;
    private Il2CppSystem.Action<RebindingOperation>? _completedAction;
    private EventSystem? _eventSystem;
    private BaseInputModule? _inputModule;
    private GameObject? _selected;
    private bool _completed, _finishing;
    private float _releaseAfter;

    internal void Begin(int index)
    {
        if (Busy) return;
        if (index is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(index));
        var events = EventSystem.current;
        if (events == null || !events.enabled || events.currentInputModule == null || !events.currentInputModule.enabled)
            throw new InvalidOperationException("The native menu input is unavailable.");
        _eventSystem = events;
        _inputModule = events.currentInputModule;
        _selected = events.currentSelectedGameObject;
        _index = index;
        // Keep the accepted key from also activating native submit/cancel/navigation.
        _inputModule.enabled = false;
    }

    internal bool Tick()
    {
        if (!Busy) return false;
        if (_finishing)
        {
            if (Time.realtimeSinceStartup >= _releaseAfter && !AnyButtonPressed())
            { Dispose(); return true; }
            return false;
        }
        if (_operation?.canceled == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true
            || (_index == 1 && UnityEngine.InputSystem.Gamepad.current?.buttonEast.wasPressedThisFrame == true))
        { Finish(); return true; }
        if (_operation == null)
        {
            // Wait for the click or submit that opened capture to be released first.
            if (AnyButtonPressed()) return false;
            _action = new InputAction("Calypso ping binding draft", InputActionType.Button);
            _action.AddBinding("<Keyboard>/space");
            _operation = _action.PerformInteractiveRebinding(0).WithExpectedControlType("Button")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Gamepad>/buttonEast")
                .WithControlsExcluding("<Gamepad>/*Stick/*")
                .WithMatchingEventsBeingSuppressed(true);
            if (_index == 0)
                _operation.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            else _operation.WithControlsHavingToMatchPath("<Gamepad>");
            _completedAction = (Il2CppSystem.Action<RebindingOperation>)(Action<RebindingOperation>)(_ => _completed = true);
            _operation.OnComplete(_completedAction).Start();
            return false;
        }
        if (!_completed) return false;
        var value = _action!.bindings[0].effectivePath;
        if (_index == 0) MouseKeyboard = value; else Gamepad = value;
        Finish();
        return true;
    }

    private void Finish()
    {
        _finishing = true;
        _releaseAfter = Time.realtimeSinceStartup + 0.1f;
        ReleaseAction();
    }

    private void ReleaseAction()
    {
        try { _operation?.Dispose(); }
        finally
        {
            _operation = null; _completedAction = null;
            _action?.Dispose(); _action = null;
        }
    }

    internal string Display(int index, string disabled)
    {
        var path = index == 0 ? MouseKeyboard : Gamepad;
        if (string.IsNullOrWhiteSpace(path)) return disabled;
        try
        {
            return InputControlPath.ToHumanReadableString(path, out _, out _,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        catch (Exception) { return path; }
    }

    private static bool AnyButtonPressed()
    {
        if (Keyboard.current?.anyKey.isPressed == true) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed
            || mouse.forwardButton.isPressed || mouse.backButton.isPressed)) return true;
        foreach (var pad in UnityEngine.InputSystem.Gamepad.all)
            if (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed
                || pad.startButton.isPressed || pad.selectButton.isPressed || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                || pad.leftTrigger.isPressed || pad.rightTrigger.isPressed || pad.leftStickButton.isPressed || pad.rightStickButton.isPressed
                || pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed || pad.dpad.right.isPressed)
                return true;
        return false;
    }

    public void Dispose()
    {
        try { ReleaseAction(); }
        finally
        {
            var events = _eventSystem; var module = _inputModule; var selected = _selected;
            _eventSystem = null; _inputModule = null; _selected = null; _index = -1; _completed = _finishing = false;
            if (module != null) module.enabled = true;
            if (events != null)
            {
                // Keep EventSystem alive for native device changes; restore focus if the input module cleared it.
                if (EventSystem.current == events && events.enabled && events.currentSelectedGameObject == null
                    && selected != null && selected.activeInHierarchy)
                    events.SetSelectedGameObject(selected);
            }
        }
    }
}
