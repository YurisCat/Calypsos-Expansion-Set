using Gameplay_Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using User;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

// Rebuild graphics from native font/frame/sprite resources. Do not clone live TMP
// meshes, overlay images or CanvasRenderer state from an already rendered widget.
internal sealed class LobbyBoxInteractionUi : IDisposable
{
    private InteractionUI? _source;
    private Canvas? _canvas;
    private GameObject? _root;
    private GameObject? _prompt;
    private TextMeshProUGUI? _input, _action;
    private Image? _point;
    private float _nextText, _nextCreate;
    private string _lastInput = "", _lastAction = "";

    internal bool Show(InteractionUI source, Camera camera, Vector3 point, bool focused, bool open)
    {
        if (_source != source || _root == null)
        {
            if (Time.realtimeSinceStartup < _nextCreate) return false;
            Dispose(); _nextCreate = Time.realtimeSinceStartup + 0.5f;
            if (!Create(source)) return false;
        }
        var screen = camera.WorldToScreenPoint(point);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height)
        { Hide(); return false; }
        if (_canvas == null || !_canvas.isActiveAndEnabled) { Hide(); return false; }
        var rootRect = _root!.GetComponent<RectTransform>();
        var canvasRect = _canvas.GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(screen.x, screen.y),
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera, out var local))
        { Hide(); return false; }
        rootRect.localPosition = new Vector3(local.x, local.y, 0);
        _root.SetActive(true);
        _point!.gameObject.SetActive(!focused);
        _prompt!.SetActive(focused);
        if (focused && Time.realtimeSinceStartup >= _nextText)
        {
            _nextText = Time.realtimeSinceStartup + 0.1f;
            var input = source.GetInputIndicatorText();
            var action = LobbyBoxText.Title + "\n" + LobbyBoxText.Pick(open ? "收起介绍" : "查看介绍",
                open ? "Close introduction" : "Read introduction");
            CopyTextStyle(source.textPrimary.inputText, _input!);
            CopyTextStyle(source.textPrimary.ActionText, _action!);
            var spriteAsset = InputIcons.SpriteAsset;
            if (_input!.spriteAsset != spriteAsset) _input.spriteAsset = spriteAsset;
            if (input != _lastInput || action != _lastAction)
            {
                _input.text = input; _action!.text = action;
                if (input != _lastInput && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
                    CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Lobby input UI] input={input}, font={_input.font?.name}, atlas={_input.fontSharedMaterial?.mainTexture?.name}, sprites={spriteAsset?.name}.");
                _lastInput = input; _lastAction = action;
            }
        }
        return true;
    }

    private bool Create(InteractionUI source)
    {
        var canvas = source.textPrimary != null ? source.textPrimary.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return false;
        var close = canvas.GetComponentInChildren<CloseInteractionOptionsUI>(true);
        if (close == null) close = source.transform.root.GetComponentInChildren<CloseInteractionOptionsUI>(true);
        if (close == null || close._closeInteractionImages == null || close._closeInteractionImages.Length == 0) return false;
        var dot = close._closeInteractionImages[0];
        if (dot == null) return false;
        _source = source; _canvas = canvas;
        _root = new GameObject("Calypso Box Native Interaction") { hideFlags = HideFlags.DontSave, layer = canvas.gameObject.layer };
        _root.SetActive(false);
        var rootRect = _root.AddComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        _prompt = new GameObject("Calypso Box Input") { hideFlags = HideFlags.DontSave, layer = canvas.gameObject.layer };
        var promptRect = _prompt.AddComponent<RectTransform>();
        promptRect.SetParent(rootRect, false);
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = Vector2.zero;
        promptRect.localScale = Vector3.one;
        var nativeFrame = source.textPrimary!.inputInteractionImage;
        if (nativeFrame != null)
        {
            var frameObject = new GameObject("Key frame") { hideFlags = HideFlags.DontSave, layer = canvas.gameObject.layer };
            frameObject.transform.SetParent(promptRect, false);
            var frame = frameObject.AddComponent<Image>();
            frame.sprite = source.textPrimary._initInputInteractionSprite != null
                ? source.textPrimary._initInputInteractionSprite : nativeFrame.sprite;
            // A null sprite on a fresh Image is a solid white rectangle.
            frame.enabled = frame.sprite != null;
            frame.color = nativeFrame.color; frame.raycastTarget = false;
            frame.material = null; // Default UI material samples this sprite, not an old overlay/atlas.
            frame.rectTransform.sizeDelta = nativeFrame.rectTransform.rect.size;
        }
        _input = CreateText("Key glyph", promptRect, new Vector2(80, 80), Vector2.zero, TextAlignmentOptions.Center);
        _input.richText = true; _input.tintAllSprites = true;
        _action = CreateText("Name and action", promptRect, new Vector2(420, 90), new Vector2(42, 0), TextAlignmentOptions.MidlineLeft);
        _action.rectTransform.pivot = new Vector2(0, 0.5f);
        CopyTextStyle(source.textPrimary.inputText, _input);
        CopyTextStyle(source.textPrimary.ActionText, _action);
        var pointObject = new GameObject("Calypso Box Interaction Point") { hideFlags = HideFlags.DontSave, layer = dot.gameObject.layer };
        pointObject.transform.SetParent(rootRect, false);
        _point = pointObject.AddComponent<Image>();
        _point.sprite = dot.sprite; _point.color = dot.color; _point.material = dot.material;
        var color = _point.color; color.a = 1; _point.color = color;
        _point.type = dot.type; _point.preserveAspect = dot.preserveAspect; _point.raycastTarget = false;
        _point.rectTransform.sizeDelta = dot.rectTransform.rect.size;
        _point.rectTransform.anchoredPosition = Vector2.zero;
        foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return true;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 size, Vector2 position, TextAlignmentOptions alignment)
    {
        var obj = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = parent.gameObject.layer };
        obj.transform.SetParent(parent, false);
        var label = obj.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false; label.maskable = false; label.richText = false;
        label.enableAutoSizing = false; label.overflowMode = TextOverflowModes.Overflow; label.alignment = alignment;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
        return label;
    }

    private static void CopyTextStyle(TMP_Text reference, TextMeshProUGUI label)
    {
        if (reference == null || reference.font == null) return;
        if (label.font != reference.font) label.font = reference.font;
        // Match the atlas to the font itself. A live widget may carry a temporary
        // material/submesh from a prior sprite or a stencil mask.
        if (label.fontSharedMaterial != reference.font.material) label.fontSharedMaterial = reference.font.material;
        label.fontSize = reference.fontSize; label.fontStyle = reference.fontStyle; label.color = reference.color;
    }

    internal void RefreshText() => _nextText = 0;
    internal void Hide() { if (_root != null) _root.SetActive(false); }
    public void Dispose()
    {
        if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
        _root = null; _source = null; _canvas = null; _prompt = null; _point = null; _input = _action = null;
        _lastInput = _lastAction = ""; _nextText = _nextCreate = 0;
    }
}
