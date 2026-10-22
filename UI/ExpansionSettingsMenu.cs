using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;
using TMPro;
using User;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using _200_Dev.UI.StartMenu;
using _200_Dev.UI.StartMenu.Menus;
using _200_Dev.UI.StartMenu.Menus.Options;
using _200_Dev.UI.UI_Navigation.NavElements;
using _200_Dev.UI.UI_Navigation.NavElements.NavBoxes;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.UI;

internal sealed class ExpansionSettingsMenu : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly ExpansionSettings _settings;
    private readonly ExpansionSettingsBindings _bindings = new();
    private MainMenu? _main;
    private MainMenusHandler? _handler;
    private NavButton? _entry, _apply;
    private UINavElement? _next;
    private GameObject? _root;
    private GameplayOptionsMenu? _page;
    private TMP_Text? _title, _hint;
    private TMP_Text? _entryLabel, _applyLabel, _backLabel, _reference;
    private readonly Toggle[] _toggles = new Toggle[4];
    private readonly TMP_Text[] _labels = new TMP_Text[4];
    private NavButton? _bindingButton;
    private TMP_Text? _bindingLabel;
    private UnityAction? _openAction, _applyAction, _bindingAction;
    private int _bindingDevice;
    private float _nextRefresh;
    private bool _failed, _saveFailed, _disposing, _loggedOpen;

    internal ExpansionSettingsMenu(CalypsoExpansionSetPlugin plugin)
    { _plugin = plugin; _settings = new ExpansionSettings(plugin.Config); }

    internal void Attach(MainMenu main)
    {
        if (_disposing) return;
        if (_main != main) { Dispose(); _main = main; _failed = false; }
        if (_failed || _entry != null || NetworkClient.active || NetworkServer.active || _plugin.RuntimeFailed) return;
        try
        {
            _handler = main.GetComponentInParent<MainMenusHandler>();
            if (_handler == null || main.optionsButton == null)
                throw new InvalidOperationException("Native main menu is unavailable.");
            _entry = UObject.Instantiate(main.optionsButton, main.optionsButton.transform.parent, false);
            _entry.name = "Calypso Expansion Settings";
            _entry.transform.SetSiblingIndex(main.optionsButton.transform.GetSiblingIndex() + 1);
            DisableLocalization(_entry.gameObject);
            _entry.Button.onClick = new Button.ButtonClickedEvent();
            _openAction = (UnityAction)(Action)Open;
            _entry.Button.onClick.AddListener(_openAction);
            _entry.SetNeighbours(new NavElementNeighbours());
            _entry.Parent = main.optionsButton.Parent;
            _entry.Parent.AddChild(_entry);
            _next = main.optionsButton.Neighbours.DownNeighbour;
            main.optionsButton.SetDownNeighbour(_entry, true);
            _entry.SetUpNeighbour(main.optionsButton, true);
            _entry.SetDownNeighbour(_next, true);
            _next?.SetUpNeighbour(_entry, true);
            _entryLabel = Label(_entry.gameObject);
            _reference = Label(main.optionsButton.gameObject);
            RefreshText();
            _entry.Parent.ReloadLayout();
            _plugin.Log.LogInfo("[Expansion settings] Main-menu entry created; page will be copied when opened.");
        }
        catch (Exception error) { Fail(error); }
    }

    private void CreatePage(GameplayOptionsMenu source)
    {
        if (source.gameObject.activeSelf)
            throw new InvalidOperationException("The native settings template must be inactive while it is copied.");
        // Keep native construction and serialized references, including the CanvasGroup on Menu.
        _page = UObject.Instantiate(source, _main!.transform.parent, false);
        _root = _page.gameObject;
        _root.name = "Calypso Expansion Settings Menu";
        _root.hideFlags = HideFlags.DontSave;
        _root.SetActive(false);
        var body = _root.transform.Find("Menu");
        if (_page.canvasGroup == null || _page.canvasGroup != body.GetComponent<CanvasGroup>())
            throw new InvalidOperationException("The copied settings CanvasGroup does not belong to its visual body.");
        DisableLocalization(_root);
        var content = body.Find("Main_Panel/Variables/Scroll View/Viewport/Content");
        var rowNames = new[] { "ShowUI_NavToggleParameter", "ShowPlayersNicks_NavToggleParameter",
            "ArachnophobeMode_NavToogleParameter", "Headbob_NavToggleParameter" };
        for (var i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (Array.IndexOf(rowNames, child.name) < 0) RemoveClone(child);
        }
        for (var i = 0; i < rowNames.Length; i++)
        {
            var row = content.Find(rowNames[i]);
            row.SetSiblingIndex(i);
            _toggles[i] = row.GetComponentInChildren<NavToggle>(true).Toggle;
            _toggles[i].onValueChanged = new Toggle.ToggleEvent();
            _labels[i] = Label(row.gameObject);
            // The page remains inactive until all callbacks and navigation references are replaced.
            var titleLayout = _labels[i].GetComponentInParent<HorizontalLayoutGroup>(true)
                ?? throw new InvalidOperationException($"Native settings title layout is missing: {rowNames[i]}");
            titleLayout.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var titleRect = titleLayout.GetComponent<RectTransform>();
            titleRect.anchorMax = new Vector2(0.75f, titleRect.anchorMax.y);
            titleRect.sizeDelta = new Vector2(0, titleRect.sizeDelta.y);
            titleLayout.childControlWidth = true; titleLayout.childForceExpandWidth = false;
            _labels[i].gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _labels[i].enableWordWrapping = false;
        }
        foreach (var index in new[] { 2, 3, 0, 1 })
            content.Find(rowNames[index]).SetAsLastSibling();
        var bindingSource = MenuManager.Instance?.OptionsMenusHandler?.controlsMenu?.rebindButton
            ?? throw new InvalidOperationException("Native rebind button is unavailable.");
        var bindingButton = _bindingButton = UObject.Instantiate(bindingSource, content, false);
        bindingButton.name = "Shared ping binding";
        DisableLocalization(bindingButton.gameObject);
        bindingButton.Button.onClick = new Button.ButtonClickedEvent();
        _bindingAction = (UnityAction)(Action)BeginBinding;
        bindingButton.Button.onClick.AddListener(_bindingAction);
        _bindingLabel = Label(bindingButton.gameObject);
        _bindingLabel.enableWordWrapping = false;
        foreach (var part in new[] { bindingButton.transform.Find("Container"), bindingButton.Button.transform, _bindingLabel.transform })
        {
            var layout = part.GetComponent<LayoutElement>() ?? part.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 0; layout.flexibleWidth = 1;
        }
        _title = Label(body.Find("Main_Panel/Panel/Title_Text").gameObject);
        var hintObject = new GameObject("Expansion settings hint") { layer = _root.layer };
        var hintRect = hintObject.AddComponent<RectTransform>();
        hintRect.SetParent(content, false);
        _hint = hintObject.AddComponent<TextMeshProUGUI>();
        _hint.font = _labels[0].font; _hint.fontSharedMaterial = _hint.font.material;
        _hint.fontSize = _labels[0].fontSize * 0.75f; _hint.fontStyle = _labels[0].fontStyle;
        _hint.color = _labels[0].color;
        _hint.enableAutoSizing = false; _hint.enableWordWrapping = true;
        _hint.alignment = TextAlignmentOptions.TopLeft; _hint.raycastTarget = false;
        hintRect.anchorMin = Vector2.zero; hintRect.anchorMax = Vector2.one;
        hintRect.pivot = new Vector2(0.5f, 0.5f); hintRect.sizeDelta = new Vector2(0, _hint.fontSize * 4);
        var hintLayout = _hint.gameObject.AddComponent<LayoutElement>();
        hintLayout.minWidth = hintLayout.preferredWidth = 0; hintLayout.flexibleWidth = 1;
        var buttons = body.Find("Buttons");
        var back = buttons.GetComponentInChildren<NavButton>(true);
        back.Button.onClick = new Button.ButtonClickedEvent();
        _apply = UObject.Instantiate(back, buttons, false);
        _apply.name = "Apply Expansion Settings";
        _apply.transform.SetAsFirstSibling();
        _apply.Button.onClick = new Button.ButtonClickedEvent();
        _applyAction = (UnityAction)(Action)Apply;
        _apply.Button.onClick.AddListener(_applyAction);
        _applyLabel = Label(_apply.gameObject); _backLabel = Label(back.gameObject);
        _page.backButton = back;
        // Pruned rows must not remain in the native navigation lists.
        var boxes = _root.GetComponentsInChildren<NavBox>(true);
        foreach (var box in boxes)
        {
            box.allElements = new Il2CppSystem.Collections.Generic.List<UINavElement>();
            box.firstSelectedElement = box.currentElement = null;
        }
        foreach (var element in _root.GetComponentsInChildren<UINavElement>(true))
        {
            element.SetNeighbours(new NavElementNeighbours());
            var parent = element == _page ? null : element.transform.parent.GetComponentInParent<NavBox>(true);
            element.Parent = parent;
            if (parent != null) parent.allElements.Add(element);
        }
        foreach (var box in boxes)
            if (box.allElements.Count > 0) box.firstSelectedElement = box.allElements[0];
        _page._navBoxes = new Il2CppReferenceArray<NavBox>(boxes.ToArray());
        _loggedOpen = false;
    }

    internal bool Owns(GameplayOptionsMenu page) => _page != null && _page == page;

    internal void RebuildActionNavigation()
    {
        var back = _page!.backButton;
        // OptionMenu rebuilds navigation on each open and only knows about its native Back button.
        back.Parent.firstSelectedElement = back.Parent.currentElement = _apply;
        var last = _bindingButton!;
        last.SetDownNeighbour(_apply, true);
        _apply!.SetUpNeighbour(last, true);
        _apply.SetDownNeighbour(back, true); _apply.SetRightNeighbour(back, true);
        back.SetUpNeighbour(_apply, true); back.SetLeftNeighbour(_apply, true);
    }

    internal void CancelBinding() => _bindings.Dispose();

    private void BeginBinding()
    {
        if (_page == null || MenuManager.CurrentMenu != _page
            || NetworkClient.active || NetworkServer.active || _plugin.RuntimeFailed) return;
        try
        {
            if (!_bindings.Busy) _bindingDevice = InputsManager.Instance?.IsGamepad == true ? 1 : 0;
            _bindings.Begin(_bindingDevice);
            RefreshText();
        }
        catch (Exception error) { Fail(error); }
    }

    private static void RemoveClone(Transform child)
    {
        child.gameObject.SetActive(false);
        child.SetParent(null, false);
        UObject.Destroy(child.gameObject);
    }

    private static void DisableLocalization(GameObject root)
    {
        foreach (var localize in root.GetComponentsInChildren<Localize>(true))
        { localize.LocalizeOnAwake = false; localize.enabled = false; }
    }

    private static TMP_Text Label(GameObject root) => root.GetComponentInChildren<TMP_Text>(true)
        ?? throw new InvalidOperationException("Native settings label is missing.");

    private void Open()
    {
        if (_handler == null || MenuManager.CurrentMenu != _main
            || NetworkClient.active || NetworkServer.active || _plugin.RuntimeFailed) return;
        try
        {
            if (_page == null)
                CreatePage(MenuManager.Instance?.OptionsMenusHandler?.gameplayMenu
                    ?? throw new InvalidOperationException("Native settings template is unavailable."));
            var values = _settings.Current;
            _toggles[0].SetIsOnWithoutNotify(values.Names);
            _toggles[1].SetIsOnWithoutNotify(values.Advice);
            _toggles[2].SetIsOnWithoutNotify(values.NaturalSpawning);
            _toggles[3].SetIsOnWithoutNotify(values.PermanentClock);
            _bindings.Dispose();
            _bindings.MouseKeyboard = values.PingMouseKeyboard; _bindings.Gamepad = values.PingGamepad;
            _saveFailed = false;
            RefreshText();
            _handler.ActivateMenu(_page!);
            if (MenuManager.CurrentMenu != _page || !_root!.activeInHierarchy || _page!.canvasGroup.alpha < 0.99f)
                throw new InvalidOperationException("The settings page did not become visible after native activation.");
            if (!_loggedOpen)
            {
                _loggedOpen = true;
                _plugin.Log.LogInfo($"[Expansion settings] Page opened: active={_root.activeInHierarchy}, "
                    + $"alpha={_page.canvasGroup.alpha}, raycasts={_page.canvasGroup.blocksRaycasts}, "
                    + $"rect={_root.GetComponent<RectTransform>().rect.size}, toggles={_toggles.Length}, bindingRows=1.");
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private void Apply()
    {
        if (_page == null || MenuManager.CurrentMenu != _page || _bindings.Busy
            || NetworkClient.active || NetworkServer.active || _plugin.RuntimeFailed) return;
        try
        {
            _settings.Apply(new(_toggles[0].isOn, _toggles[1].isOn, _toggles[2].isOn, _toggles[3].isOn,
                _bindings.MouseKeyboard, _bindings.Gamepad));
        }
        catch (Exception error)
        {
            _saveFailed = true; RefreshText();
            _plugin.Log.LogError($"[Expansion settings] Could not save settings; previous values retained: {error}");
            return;
        }
        try { _handler!.Back(); }
        catch (Exception error) { Fail(error); }
    }

    internal void Tick()
    {
        if (_entry == null || _failed) return;
        try
        {
            if (_main == null) { Dispose(); return; }
            if (_page != null && MenuManager.CurrentMenu == _page
                && (NetworkClient.active || NetworkServer.active || _plugin.RuntimeFailed))
                _handler!.Back();
            if (_bindings.Busy)
            {
                if (_page == null || MenuManager.CurrentMenu != _page) _bindings.Dispose();
                else if (_bindings.Tick()) RefreshText();
            }
            if (MenuManager.CurrentMenu != _main && (_page == null || MenuManager.CurrentMenu != _page)) return;
            if (Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 0.5f;
            _entry.SetInteractable(!NetworkClient.active && !NetworkServer.active && !_plugin.RuntimeFailed);
            RefreshText();
        }
        catch (Exception error) { Fail(error); }
    }

    private void RefreshText()
    {
        var text = ExpansionSettingsText.For(IdolHoverInfo.CurrentLanguage);
        var reference = _reference!;
        SetLabel(_entryLabel!, text.EntryTitle, reference);
        _entryLabel!.fontSize = reference.fontSize;
        if (_page == null) return;
        SetLabel(_title!, text.PageTitle, reference);
        SetLabel(_labels[0], text.Names, reference, true); SetLabel(_labels[1], text.Advice, reference, true);
        SetLabel(_labels[2], text.Natural, reference, true); SetLabel(_labels[3], text.Clock, reference, true);
        _bindingDevice = _bindings.Busy ? _bindings.DeviceIndex : InputsManager.Instance?.IsGamepad == true ? 1 : 0;
        SetLabel(_bindingLabel!, (_bindingDevice == 0 ? text.PingMouse : text.PingGamepad) + "  ["
            + (_bindings.IsRecording(_bindingDevice) ? "…" : _bindings.Display(_bindingDevice, text.Disabled)) + "]", reference, true);
        SetLabel(_applyLabel!, text.Apply, reference);
        SetLabel(_backLabel!, text.Back, reference);
        SetLabel(_hint!, _bindings.Busy ? (_bindingDevice == 0 ? text.RebindMouseHint : text.RebindGamepadHint)
            : text.SaveFailed, reference);
        _hint!.gameObject.SetActive(_saveFailed || _bindings.Busy);
    }

    private static void SetLabel(TMP_Text label, string text, TMP_Text reference, bool fit = false)
    {
        if (label.font != reference.font)
        { label.font = reference.font; label.fontSharedMaterial = reference.font.material; }
        if (label.text != text) label.text = text;
        if (!fit) { label.enableAutoSizing = false; return; }
        if (label.enableAutoSizing) return;
        label.fontSizeMax = label.fontSize; label.fontSizeMin = label.fontSize * 0.75f;
        label.enableAutoSizing = true;
    }

    private void Fail(Exception error)
    {
        _failed = true;
        _plugin.Log.LogError($"[Expansion settings] Menu disabled for this scene after an error: {error}");
        Dispose();
    }

    public void Dispose()
    {
        _disposing = true;
        try
        {
            Cleanup(_bindings.Dispose);
            Cleanup(() => { if (_page != null && MenuManager.CurrentMenu == _page && _handler != null) _handler.Back(); });
            Cleanup(() =>
            {
                if (_main != null && MenuManager.CurrentMenu == _main && !_main.gameObject.activeSelf)
                    _main.ToggleSelf(true);
            });
            if (_entry != null)
            {
                Cleanup(() =>
                {
                    if (_main != null && _main.optionsButton != null && _main.optionsButton.Neighbours.DownNeighbour == _entry)
                        _main.optionsButton.SetDownNeighbour(_next, true);
                    if (_next != null && _next.Neighbours.UpNeighbour == _entry && _main != null)
                        _next.SetUpNeighbour(_main.optionsButton, true);
                });
                Cleanup(() => _entry.Parent?.RemoveChild(_entry));
                Cleanup(() => { _entry.gameObject.SetActive(false); UObject.Destroy(_entry.gameObject); });
            }
            Cleanup(() => { if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); } });
        }
        finally
        {
            _entry = _apply = null; _page = null; _root = null; _handler = null; _next = null;
            _title = _hint = _entryLabel = _applyLabel = _backLabel = _reference = null;
            _bindingButton = null; _bindingLabel = null;
            _openAction = _applyAction = _bindingAction = null;
            Array.Clear(_toggles); Array.Clear(_labels);
            _disposing = false;
        }
    }

    private void Cleanup(Action action)
    {
        try { action(); }
        catch (Exception error) { _plugin.Log.LogError($"[Expansion settings] Menu cleanup failed: {error}"); }
    }
}

// Only the owned clone bypasses vanilla settings callbacks. Native menus remain untouched.
[HarmonyPatch]
internal static class ExpansionSettingsPagePatches
{
    private static bool Owns(GameplayOptionsMenu page) =>
        CalypsoExpansionSetPlugin.Instance?.SettingsMenu?.Owns(page) == true;

    private static unsafe void InvokeBase<T>(string name, GameplayOptionsMenu page)
    {
        // The generated virtual wrappers dispatch back to GameplayOptionsMenu; invoke the base directly.
        var method = IL2CPP.il2cpp_class_get_method_from_name(Il2CppClassPointerStore<T>.NativeClassPtr, name, 0);
        if (method == IntPtr.Zero)
            throw new InvalidOperationException($"Native settings lifecycle method is unavailable: {typeof(T).Name}.{name}.");
        var exception = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(method, page.Pointer, null, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GameplayOptionsMenu), nameof(GameplayOptionsMenu.OnEnable))]
    private static bool OnEnable(GameplayOptionsMenu __instance)
    {
        if (!Owns(__instance)) return true;
        InvokeBase<NavBox>(nameof(NavBox.OnEnable), __instance);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GameplayOptionsMenu), nameof(GameplayOptionsMenu.OnToggle))]
    private static bool OnToggle(GameplayOptionsMenu __instance)
    {
        if (!Owns(__instance)) return true;
        InvokeBase<OptionMenu>(nameof(OptionMenu.OnToggle), __instance);
        CalypsoExpansionSetPlugin.Instance!.SettingsMenu!.RebuildActionNavigation();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GameplayOptionsMenu), nameof(GameplayOptionsMenu.OnUntoggle))]
    private static bool OnUntoggle(GameplayOptionsMenu __instance)
    {
        if (!Owns(__instance)) return true;
        CalypsoExpansionSetPlugin.Instance!.SettingsMenu!.CancelBinding();
        InvokeBase<OptionMenu>(nameof(OptionMenu.OnUntoggle), __instance);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GameplayOptionsMenu), nameof(GameplayOptionsMenu.RefreshParameters))]
    private static bool RefreshParameters(GameplayOptionsMenu __instance) => !Owns(__instance);
}

[HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OnToggle))]
internal static class ExpansionSettingsMainMenuPatch
{
    private static void Postfix(MainMenu __instance) => CalypsoExpansionSetPlugin.Instance?.SettingsMenu?.Attach(__instance);
}
