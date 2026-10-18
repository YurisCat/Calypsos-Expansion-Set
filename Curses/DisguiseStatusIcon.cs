using System.Runtime.InteropServices;
using UnityEngine;
using _200_Dev.UI.Alteration;

namespace CalypsoExpansionSet.Curses;

// Use the vanilla status slot and layout, without putting a private alteration
// type into the native runner or its value-type slot list.
internal sealed class DisguiseStatusIcon : IDisposable
{
    private const int Size = 256;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private PlayerAlterationUI? _ui;
    private GameObject? _root;
    private Texture2D? _texture;
    private Sprite? _sprite;
    private IntPtr _failedUi;
    private bool _disposed;

    internal DisguiseStatusIcon(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Update(PlayerAlterationUI? ui, bool active)
    {
        if (_disposed) return;
        if (!active) { Clear(); _failedUi = IntPtr.Zero; return; }
        // A dead player's HUD may temporarily be unavailable. The curse still
        // exists; keep the slot until its HUD is replaced or the snapshot expires.
        if (ui == null) return;
        if (_ui != null && _ui.Pointer == ui.Pointer && _root != null) return;
        Clear();
        if (_failedUi == ui.Pointer || ui.alterationSlotPrefab == null) return;
        try
        {
            EnsureArtwork();
            _ui = ui;
            _root = UnityEngine.Object.Instantiate(ui.alterationSlotPrefab, ui.transform);
            _root.name = "Calypso disguise status";
            _root.hideFlags = HideFlags.DontSave;
            var slot = _root.GetComponent<PlayerAlterationSlot>();
            if (slot == null || slot.alterationIcon == null)
                throw new InvalidDataException("Vanilla alteration slot has no icon image.");
            slot.alterationIcon.sprite = _sprite;
            slot.alterationIcon.enabled = true;
            slot.alterationIcon.gameObject.SetActive(true);
            _root.SetActive(true);
            ui._layoutGroupRebuilder?.ReloadLayout();
        }
        catch (Exception error)
        {
            Clear();
            ReleaseArtwork();
            _failedUi = ui.Pointer;
            _plugin.Log.LogError($"Disguise status icon could not be created: {error}");
        }
    }

    private void EnsureArtwork()
    {
        if (_sprite != null) return;
        using var stream = typeof(DisguiseStatusIcon).Assembly.GetManifestResourceStream("CalypsoExpansionSet.Curses.disguise.rgba")
            ?? throw new InvalidDataException("Missing embedded disguise icon.");
        using var reader = new BinaryReader(stream);
        var pixels = reader.ReadBytes(Size * Size * 4);
        if (pixels.Length != Size * Size * 4 || stream.ReadByte() != -1)
            throw new InvalidDataException("Invalid disguise icon size.");
        _texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false)
        { name = "Calypso disguise icon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { _texture.LoadRawTextureData(handle.AddrOfPinnedObject(), pixels.Length); }
        finally { handle.Free(); }
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    internal void Clear()
    {
        if (_root != null)
        {
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
            if (_ui != null) _ui._layoutGroupRebuilder?.ReloadLayout();
        }
        _root = null;
        _ui = null;
    }

    private void ReleaseArtwork()
    {
        if (_sprite != null) UnityEngine.Object.Destroy(_sprite);
        if (_texture != null) UnityEngine.Object.Destroy(_texture);
        _sprite = null; _texture = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        ReleaseArtwork();
    }
}
