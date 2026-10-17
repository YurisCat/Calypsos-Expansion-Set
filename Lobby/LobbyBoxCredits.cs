using System.Runtime.InteropServices;
using CalypsoExpansionSet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyBoxCredits : IDisposable
{
    private readonly TextMeshProUGUI[] _roles = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] _names = new TextMeshProUGUI[4];
    private readonly RectTransform[] _developerRows = new RectTransform[2];
    private readonly Image[] _icons = new Image[3];
    private RectTransform? _root;
    private TextMeshProUGUI? _title;
    private Texture2D? _texture;
    private Sprite? _sprite;
    private UiLanguage? _language;
    private float _bodySize;
    private Vector2 _layoutSize;
    private static readonly Color TitleColor = new Color32(205, 187, 145, 255);
    private static readonly Color RoleColor = new Color32(174, 173, 169, 255);
    private static readonly Color NameColor = new Color32(231, 226, 214, 255);

    internal void Create(Transform parent, TMP_Text reference, float bodySize)
    {
        _bodySize = bodySize;
        var root = _root = Child("Fixed credits", parent);
        root.anchorMin = new Vector2(150f / 1920, 90f / 1080);
        root.anchorMax = new Vector2(940f / 1920, 220f / 1080);
        _title = Label("Credits heading", root, reference, bodySize * 0.72f, TitleColor, 0, 1, 0.78f, 1);
        for (var i = 0; i < 3; i++)
        {
            var bottom = (2 - i) * 0.25f;
            _roles[i] = Label("Credit role", root, reference, bodySize * 0.68f, RoleColor, 0, 0, bottom, bottom + 0.25f);
            _roles[i].rectTransform.pivot = new Vector2(0, 0.5f);
        }
        for (var i = 0; i < 2; i++)
        {
            var row = _developerRows[i] = Child("Developer", root);
            row.anchorMin = new Vector2(0, 0.5f);
            row.anchorMax = new Vector2(0, 0.75f);
            row.pivot = new Vector2(0, 0.5f);
            _names[i] = Label("Developer name", row, reference, bodySize * 0.72f, NameColor, 0, 1, 0, 1);
            _icons[i] = CreateIconImage(row);
        }
        _names[2] = Label("Modeling name", root, reference, bodySize * 0.72f, NameColor, 0, 1, 0.25f, 0.5f);
        _icons[2] = CreateIconImage(_names[2].transform);
        _names[3] = Label("Artwork name", root, reference, bodySize * 0.72f, NameColor, 0, 1, 0, 0.25f);
        _names[3].text = "Charrant";
    }

    internal void SetLanguage(UiLanguage language, TMP_Text reference)
    {
        if (_root == null || !_root.gameObject.activeInHierarchy || reference == null || reference.font == null) return;
        var size = _root.rect.size;
        var bodySize = reference.fontSize * 0.8f;
        if (size.x <= 0 || size.y <= 0 || (_language == language && _layoutSize == size
            && _title!.font == reference.font && _title.fontSharedMaterial == reference.font.material
            && _title.fontStyle == reference.fontStyle && _bodySize == bodySize)) return;
        _bodySize = bodySize;
        var chinese = UiLanguages.IsChinese(language);
        var text = LobbyBoxText.For(language);
        _title!.text = text.Credits;
        _roles[0].text = text.Development;
        _roles[1].text = text.Modeling;
        _roles[2].text = text.Artwork;
        MatchFont(_title, reference);
        foreach (var role in _roles) MatchFont(role, reference);
        for (var i = 0; i < _names.Length; i++)
        {
            _names[i].text = LobbyBoxText.CreditName(i, language);
            MatchFont(_names[i], reference);
        }
        if (_sprite == null) CreateIcon();
        SetFontScale(1);
        var rowHeight = 0f;
        foreach (var role in _roles) rowHeight = Mathf.Max(rowHeight, role.GetPreferredValues(role.text).y);
        foreach (var name in _names) rowHeight = Mathf.Max(rowHeight, name.GetPreferredValues(name.text).y);
        // Fit actual font metrics into the fixed footer, including CJK fallback fonts.
        var scale = Mathf.Min(1, Mathf.Max(1, size.y * 0.22f - 2) / Mathf.Max(1, _title.GetPreferredValues(_title.text).y));
        scale = Mathf.Min(scale, Mathf.Max(1, size.y * 0.25f - 2) / Mathf.Max(1, rowHeight));
        // Long translated roles and both developer names share the same row.
        // Fit its measured width too; height alone can leave the second name off-screen.
        var widestRole = 0f;
        foreach (var role in _roles) widestRole = Mathf.Max(widestRole, role.GetPreferredValues(role.text).x);
        var developersWidth = _names[0].GetPreferredValues(_names[0].text).x
            + _names[1].GetPreferredValues(_names[1].text).x + _bodySize * 0.65f
            + (chinese ? (_names[0].fontSize + _names[1].fontSize) * 1.2f : 0);
        var widestName = Mathf.Max(developersWidth, Mathf.Max(
            _names[2].GetPreferredValues(_names[2].text).x + (chinese ? 0 : _names[2].fontSize * 1.2f),
            _names[3].GetPreferredValues(_names[3].text).x));
        var widestRow = Mathf.Max(_title.GetPreferredValues(_title.text).x, widestRole + _bodySize * 0.45f + widestName);
        scale = Mathf.Min(scale, Mathf.Max(1, size.x - 8) / Mathf.Max(1, widestRow));
        SetFontScale(scale);
        var roleWidth = 0f;
        foreach (var role in _roles) roleWidth = Mathf.Max(roleWidth, role.GetPreferredValues(role.text).x);
        roleWidth += 2;
        foreach (var role in _roles) role.rectTransform.sizeDelta = new Vector2(roleWidth, 0);
        var namesLeft = roleWidth + _bodySize * scale * 0.45f;
        _names[2].rectTransform.offsetMin = _names[3].rectTransform.offsetMin = new Vector2(namesLeft, 0);
        var left = namesLeft;
        for (var i = 0; i < _developerRows.Length; i++)
        {
            var nameWidth = _names[i].GetPreferredValues(_names[i].text).x;
            var width = nameWidth + PlaceIcon(i, chinese, nameWidth) + 1;
            _developerRows[i].sizeDelta = new Vector2(width, 0);
            _developerRows[i].anchoredPosition = new Vector2(left, 0);
            _names[i].rectTransform.offsetMin = Vector2.zero;
            left += width + _bodySize * scale * 0.65f;
        }
        PlaceIcon(2, !chinese, _names[2].GetPreferredValues(_names[2].text).x);
        _language = language; _layoutSize = size;
    }

    private static void MatchFont(TMP_Text text, TMP_Text reference)
    {
        text.font = reference.font;
        text.fontSharedMaterial = reference.font.material;
        text.fontStyle = reference.fontStyle;
    }

    private static Image CreateIconImage(Transform parent)
    {
        var icon = Child("Bilibili", parent).gameObject.AddComponent<Image>();
        icon.color = NameColor; icon.raycastTarget = false; icon.preserveAspect = true;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
        return icon;
    }

    private float PlaceIcon(int index, bool visible, float nameWidth)
    {
        var icon = _icons[index];
        var size = _names[index].fontSize;
        icon.sprite = _sprite;
        icon.gameObject.SetActive(visible);
        icon.rectTransform.sizeDelta = Vector2.one * size;
        icon.rectTransform.anchoredPosition = new Vector2(nameWidth + size * 0.7f, 0);
        return visible ? size * 1.2f : 0;
    }

    private void SetFontScale(float scale)
    {
        _title!.fontSize = _bodySize * 0.72f * scale;
        foreach (var role in _roles) role.fontSize = _bodySize * 0.68f * scale;
        foreach (var name in _names) name.fontSize = _bodySize * 0.72f * scale;
    }

    private void CreateIcon()
    {
        // Rasterized from Assets/LobbyBox/bilibili-line.svg; raw RGBA avoids native LoadImage wrappers.
        const int size = 96;
        using var stream = typeof(LobbyBoxCredits).Assembly.GetManifestResourceStream("CalypsoExpansionSet.LobbyBox.bilibili.rgba")
            ?? throw new InvalidDataException("Missing embedded Bilibili icon.");
        using var reader = new BinaryReader(stream);
        var pixels = reader.ReadBytes(size * size * 4);
        if (pixels.Length != size * size * 4 || stream.ReadByte() != -1)
            throw new InvalidDataException("Invalid Bilibili icon size.");
        _texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            { name = "Calypso Bilibili icon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { _texture.LoadRawTextureData(handle.AddrOfPinnedObject(), pixels.Length); }
        finally { handle.Free(); }
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = parent.gameObject.layer };
        var rect = child.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_Text reference, float size, Color color,
        float left, float right, float bottom, float top)
    {
        var text = Child(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = reference.font; text.fontSharedMaterial = reference.font.material;
        text.fontStyle = reference.fontStyle; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.richText = false; text.raycastTarget = false; text.maskable = false;
        text.enableAutoSizing = false; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.anchorMin = new Vector2(left, bottom);
        text.rectTransform.anchorMax = new Vector2(right, top);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    public void Dispose()
    {
        if (_sprite != null) UObject.Destroy(_sprite);
        if (_texture != null) UObject.Destroy(_texture);
        _sprite = null; _texture = null; _root = null; _title = null; _language = null; _layoutSize = Vector2.zero;
        Array.Clear(_roles, 0, _roles.Length);
        Array.Clear(_names, 0, _names.Length);
        Array.Clear(_developerRows, 0, _developerRows.Length);
        Array.Clear(_icons, 0, _icons.Length);
    }
}
