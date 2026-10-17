using Entities.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using User;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyBoxReadingUi : IDisposable
{
    private GameObject? _root;
    private CanvasGroup? _group;
    private TextMeshProUGUI? _text;
    private TextMeshProUGUI? _scrollHint;
    private PlayerEntity? _player;
    private RectTransform? _viewport;
    private LobbyBoxCredits? _credits;
    private Vector2 _viewSize;
    private float _scroll, _maxScroll, _lineHeight;
    private double _wheelSampleTime = -1;
    private bool _moveArmed;
    private float _nextText;
    private float _fadeDuration;
    private bool _closing;
    private int _lastFrame = -1;
    private string _content = "";
    internal bool Ready => _root != null && _root.activeInHierarchy;

    internal bool Create(PlayerEntity player)
    {
        Dispose();
        var source = player.SubtitleUI;
        var reference = source?._text?.Text;
        var gradient = source != null ? source.transform.Find("BackgroundGradient")?.GetComponent<Image>() : null;
        if (reference == null || reference.font == null || gradient == null || gradient.sprite == null) return false;
        _player = player;
        _root = new GameObject("Calypso Box Reading") { hideFlags = HideFlags.DontSave, layer = source!.gameObject.layer };
        _root.SetActive(false);
        var rect = _root.AddComponent<RectTransform>();
        rect.SetParent(source.transform.parent, false);
        rect.SetAsFirstSibling(); // Native HUD and important messages remain above the reading overlay.
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
        _group = _root.AddComponent<CanvasGroup>();
        _group.alpha = 0; _group.interactable = false; _group.blocksRaycasts = false;
        _fadeDuration = float.IsFinite(source._fadeDuration) && source._fadeDuration > 0 ? source._fadeDuration : 0.25f;
        var background = Child("Mirrored native gradient").AddComponent<Image>();
        background.sprite = gradient.sprite; background.color = gradient.color; background.raycastTarget = false;
        var bgRect = background.rectTransform;
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = new Vector2(1470f / 1920, 1);
        bgRect.sizeDelta = bgRect.anchoredPosition = Vector2.zero;
        // Flip only the native horizontal gradient. Text and the global vignette stay independent.
        bgRect.localScale = new Vector3(-1, 1, 1);
        _viewport = Child("Introduction viewport").AddComponent<RectTransform>();
        _viewport.anchorMin = new Vector2(150f / 1920, 270f / 1080);
        _viewport.anchorMax = new Vector2(940f / 1920, 1 - 170f / 1080);
        _viewport.offsetMin = _viewport.offsetMax = Vector2.zero;
        _viewport.gameObject.AddComponent<RectMask2D>();
        _text = Child("Introduction", _viewport).AddComponent<TextMeshProUGUI>();
        _text.font = reference.font; _text.fontSharedMaterial = reference.font.material;
        _text.fontSize = reference.fontSize * 0.8f; _text.fontStyle = reference.fontStyle; _text.color = reference.color;
        _text.raycastTarget = false; _text.maskable = true; _text.richText = false;
        _text.alignment = TextAlignmentOptions.TopLeft; _text.enableAutoSizing = false;
        _text.enableWordWrapping = true; _text.overflowMode = TextOverflowModes.Overflow;
        _text.rectTransform.anchorMin = new Vector2(0, 1);
        _text.rectTransform.anchorMax = Vector2.one;
        _text.rectTransform.pivot = new Vector2(0.5f, 1);
        _text.rectTransform.anchoredPosition = Vector2.zero;
        _scrollHint = Child("Scroll hint").AddComponent<TextMeshProUGUI>();
        _scrollHint.font = reference.font; _scrollHint.fontSharedMaterial = reference.font.material;
        _scrollHint.fontStyle = reference.fontStyle; _scrollHint.fontSize = _text.fontSize * 0.7f;
        _scrollHint.color = new Color32(174, 173, 169, 255);
        _scrollHint.raycastTarget = false; _scrollHint.maskable = false; _scrollHint.richText = false;
        _scrollHint.enableWordWrapping = false; _scrollHint.enableAutoSizing = true;
        _scrollHint.fontSizeMax = _scrollHint.fontSize; _scrollHint.fontSizeMin = Mathf.Min(12, _scrollHint.fontSize);
        _scrollHint.overflowMode = TextOverflowModes.Ellipsis;
        _scrollHint.rectTransform.anchorMin = new Vector2(150f / 1920, 235f / 1080);
        _scrollHint.rectTransform.anchorMax = new Vector2(940f / 1920, 260f / 1080);
        _scrollHint.rectTransform.offsetMin = _scrollHint.rectTransform.offsetMax = Vector2.zero;
        _scrollHint.gameObject.SetActive(false);
        _credits = new LobbyBoxCredits();
        _credits.Create(_root.transform, reference, _text.fontSize);
        _root.SetActive(true);
        Tick();
        return true;
    }

    private GameObject Child(string name, Transform? parent = null)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = _root!.layer };
        child.transform.SetParent(parent != null ? parent : _root.transform, false);
        return child;
    }

    internal void Tick(bool allowScroll = false)
    {
        if (_root == null) return;
        if (!_root.activeInHierarchy) { Dispose(); return; }
        if (_lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        if (_group != null)
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _closing ? 0 : 1, Time.unscaledDeltaTime / _fadeDuration);
            if (_closing && _group.alpha <= 0) { Dispose(); return; }
        }
        if (_closing) return;
        if (_text == null || _viewport == null) return;
        if (Time.realtimeSinceStartup >= _nextText)
        {
            _nextText = Time.realtimeSinceStartup + 0.25f;
            var reference = _player?.SubtitleUI?._text?.Text;
            if (reference == null || reference.font == null) { Dispose(); return; }
            var strings = LobbyBoxText.Current;
            var content = strings.Introduction;
            _credits?.SetLanguage(LobbyBoxText.Language, reference);
            // The native subtitle font can change while this overlay is open.
            // Refresh only owned labels and recompute wrapping with the new metrics.
            if (_text.font != reference.font || _text.fontSharedMaterial != reference.font.material
                || _text.fontStyle != reference.fontStyle || _text.fontSize != reference.fontSize * 0.8f)
            {
                _text.font = reference.font; _text.fontSharedMaterial = reference.font.material;
                _text.fontStyle = reference.fontStyle; _text.fontSize = reference.fontSize * 0.8f;
                _scrollHint!.font = reference.font; _scrollHint.fontSharedMaterial = reference.font.material;
                _scrollHint.fontStyle = reference.fontStyle;
                _scrollHint.fontSizeMax = _text.fontSize * 0.7f;
                _scrollHint.fontSizeMin = Mathf.Min(12, _scrollHint.fontSizeMax);
                _viewSize = Vector2.zero;
            }
            if (_content != content)
            {
                _text.text = content; _content = content;
                _scroll = 0; _viewSize = Vector2.zero; _moveArmed = false;
                _wheelSampleTime = Mouse.current?.lastUpdateTime ?? -1;
                _scrollHint!.text = strings.Scroll;
            }
        }
        var size = _viewport.rect.size;
        if (size.x <= 0 || size.y <= 0) return;
        if (_viewSize != size)
        {
            var height = Mathf.Max(size.y, _text.GetPreferredValues(_content, Mathf.Max(1, size.x - 12), Mathf.Infinity).y);
            _text.rectTransform.sizeDelta = new Vector2(-12, height);
            _maxScroll = Mathf.Max(0, height - size.y);
            _scroll = Mathf.Clamp(_scroll, 0, _maxScroll);
            // Measure line spacing in the text's local units, including the font's own leading.
            _lineHeight = _text.GetPreferredValues("A\nA", Mathf.Infinity, Mathf.Infinity).y
                - _text.GetPreferredValues("A", Mathf.Infinity, Mathf.Infinity).y;
            if (!float.IsFinite(_lineHeight) || _lineHeight <= 0) _lineHeight = _text.fontSize;
            _viewSize = size;
        }
        _scrollHint!.gameObject.SetActive(allowScroll && _maxScroll > 0);
        if (allowScroll && _maxScroll > 0) Scroll();
        _text.rectTransform.anchoredPosition = new Vector2(0, _scroll);
    }

    private void Scroll()
    {
        // Poll the bound action directly: native movement is paused and held-idol Move overrides do not apply here.
        var action = InputsManager.Instance?.onMove;
        var vertical = action?.enabled == true ? action.ReadValue<Vector2>().y : 0;
        if (!float.IsFinite(vertical) || Mathf.Abs(vertical) < 0.2f) { vertical = 0; _moveArmed = true; }
        var wheel = 0f;
        var mouse = Mouse.current;
        if (mouse != null && mouse.lastUpdateTime != _wheelSampleTime)
        {
            // A device update can span several rendered frames; consume its delta only once.
            _wheelSampleTime = mouse.lastUpdateTime;
            wheel = mouse.scroll.ReadValue().y;
        }
        if (!float.IsFinite(wheel)) wheel = 0;
        var delta = 0f;
        if (wheel != 0)
        {
            // Input System can normalize each notch to 1; its own unit also covers platform-specific ranges.
            var perTick = InputSystem.scrollWheelDeltaPerTick;
            if (!float.IsFinite(perTick) || perTick <= 0) perTick = 1;
            delta = -(wheel / perTick) * _lineHeight * 2;
        }
        if (_moveArmed) delta -= Mathf.Clamp(vertical, -1, 1) * _text!.fontSize * 10 * Time.unscaledDeltaTime;
        _scroll = Mathf.Clamp(_scroll + delta, 0, _maxScroll);
    }

    internal void FadeOut()
    {
        if (!Ready || _group == null || _group.alpha <= 0) { Dispose(); return; }
        if (_scrollHint != null) _scrollHint.gameObject.SetActive(false);
        _closing = true;
    }

    public void Dispose()
    {
        if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
        _credits?.Dispose(); _credits = null;
        _viewport = null; _scrollHint = null; _viewSize = Vector2.zero; _scroll = _maxScroll = _lineHeight = 0; _moveArmed = false;
        _wheelSampleTime = -1;
        _root = null; _text = null; _player = null; _group = null; _content = ""; _nextText = 0; _lastFrame = -1; _closing = false;
    }
}
