using Entities.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyBoxReadingUi : IDisposable
{
    private GameObject? _root;
    private CanvasGroup? _group;
    private TextMeshProUGUI? _text;
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
        _text = Child("Introduction").AddComponent<TextMeshProUGUI>();
        _text.font = reference.font; _text.fontSharedMaterial = reference.font.material;
        _text.fontSize = reference.fontSize * 0.8f; _text.fontStyle = reference.fontStyle; _text.color = reference.color;
        _text.raycastTarget = false; _text.maskable = false; _text.richText = false;
        _text.alignment = TextAlignmentOptions.TopLeft; _text.enableAutoSizing = false;
        _text.enableWordWrapping = true; _text.overflowMode = TextOverflowModes.Overflow;
        // Reference area: x=150..940, y=170..890 from the top at 1920x1080.
        // Relative anchors preserve the HUD margin and the gap before the box on other aspect ratios.
        _text.rectTransform.anchorMin = new Vector2(150f / 1920, 190f / 1080);
        _text.rectTransform.anchorMax = new Vector2(940f / 1920, 1 - 170f / 1080);
        _text.rectTransform.offsetMin = _text.rectTransform.offsetMax = Vector2.zero;
        _root.SetActive(true);
        Tick();
        return true;
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = _root!.layer };
        child.transform.SetParent(_root.transform, false);
        return child;
    }

    internal void Tick()
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
        if (_text == null || Time.realtimeSinceStartup < _nextText) return;
        _nextText = Time.realtimeSinceStartup + 0.25f;
        var content = LobbyBoxText.Introduction;
        if (_content != content) { _text.text = content; _content = content; }
    }

    internal void FadeOut()
    {
        if (!Ready || _group == null || _group.alpha <= 0) { Dispose(); return; }
        _closing = true;
    }

    public void Dispose()
    {
        if (_root != null) { _root.SetActive(false); UObject.Destroy(_root); }
        _root = null; _text = null; _group = null; _content = ""; _nextText = 0; _lastFrame = -1; _closing = false;
    }
}
