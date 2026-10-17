using Entities.Player;
using EnvironmentElements;
using TMPro;
using UnityEngine;
using _200_Dev.Portrait;

namespace CalypsoExpansionSet.Lobby;

// Tint only nickname RGB; native materials, text effects and alpha stay owned by the game.
internal sealed class PlayerPresenceNames : IDisposable
{
    internal static readonly Color InsideColor = new(0.05f, 0.9f, 1f, 1f);
    private sealed record NameTint(PlayerPortrait Portrait, PlayerEntity Owner, TextMeshProUGUI Text)
    {
        internal Color Original;
        internal bool Applied;

        internal void Apply()
        {
            var current = Text.color;
            if (!Applied || !HasTint(current)) Original = current;
            if (HasTint(current)) return;
            Applied = true;
            Text.color = WithRgb(InsideColor, current.a);
        }

        internal void Restore()
        {
            if (!Applied || Text == null) return;
            var current = Text.color;
            // Preserve fades and a newer RGB write made by the game or another mod.
            if (HasTint(current)) Text.color = WithRgb(Original, current.a);
            Applied = false;
        }
    }
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Dictionary<int, NameTint> _names = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _removed = new();
    private Room? _room;
    private float _nextUpdate;
    private bool _disposed, _failed;

    internal PlayerPresenceNames(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    private static bool HasTint(Color color) =>
        color.r == InsideColor.r && color.g == InsideColor.g && color.b == InsideColor.b;

    private static Color WithRgb(Color rgb, float alpha) => new(rgb.r, rgb.g, rgb.b, alpha);

    internal void Tick()
    {
        if (_disposed || _failed) return;
        try
        {
            var room = Mirror.NetworkClient.ready ? Room.GetStartRoom() : null;
            if (_room != room) { Clear(); _room = room; }
            var portraits = room != null ? PortraitManager.Instance?.Portrait : null;
            if (portraits == null || _plugin.RuntimeFailed || !Mirror.NetworkClient.isConnected)
            { Clear(); return; }
            if (Time.realtimeSinceStartup < _nextUpdate) return;
            _nextUpdate = Time.realtimeSinceStartup + 0.1f;
            _seen.Clear();
            for (var index = 0; index < portraits.Count; index++)
            {
                var portrait = portraits[index];
                var owner = portrait != null ? portrait.CurrentPortraitOwner : null;
                var source = portrait != null ? portrait.portraitRenderer : null;
                var text = portrait != null ? portrait._textMesh : null;
                if (portrait == null || !portrait.isActiveAndEnabled || source == null || owner == null
                    || text == null || !text.isActiveAndEnabled || !source.enabled
                    || !source.gameObject.activeInHierarchy) continue;
                bool inside = false;
                if (_plugin.RoomStatus?.InCurrentStartRoom(owner, room!, out inside) != true || !inside) continue;
                var key = portrait.GetInstanceID();
                _seen.Add(key);
                if (_names.TryGetValue(key, out var name)
                    && (name.Portrait != portrait || name.Owner != owner || name.Text != text))
                { Restore(name); _names.Remove(key); name = null; }
                if (name == null) _names[key] = name = new NameTint(portrait, owner, text);
                name.Apply();
            }
            _removed.Clear();
            foreach (var key in _names.Keys) if (!_seen.Contains(key)) _removed.Add(key);
            foreach (var key in _removed) { Restore(_names[key]); _names.Remove(key); }
        }
        catch (Exception error)
        {
            _failed = true;
            Clear();
            _plugin.Log.LogError($"Player presence name tint disabled after an error; native portraits remain active: {error}");
        }
    }

    private void Restore(NameTint name)
    {
        try { name.Restore(); }
        catch (Exception error)
        { _plugin.Log.LogWarning($"[Player presence] could not restore nickname color: {error.Message}"); }
    }

    private void Clear()
    {
        foreach (var name in _names.Values) Restore(name);
        _names.Clear();
        _room = null;
        _nextUpdate = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
