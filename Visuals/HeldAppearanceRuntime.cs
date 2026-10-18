using Entities.Player;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using User;

namespace CalypsoExpansionSet.Visuals;

internal interface IPlayerAppearance : IDisposable
{
    bool Matches(PlayerEntity player);
    void Render();
}

// Every compatible client derives simple appearances from native held-item
// state, including observers and late joiners. There is no local-owner filter.
internal sealed class HeldAppearanceRuntime : IdolContentRuntime
{
    private static readonly HashSet<HeldAppearanceRuntime> Instances = new();
    private sealed record State(PlayerEntity Player, ScoreTool Idol, IPlayerAppearance Appearance);
    private readonly ulong _configId;
    private readonly Func<PlayerEntity, IPlayerAppearance?> _create;
    private readonly Dictionary<uint, State> _states = new();
    private readonly Dictionary<uint, IntPtr> _faulted = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _removed = new();
    private bool _disposed;

    internal HeldAppearanceRuntime(ulong configId, Func<PlayerEntity, IPlayerAppearance?> create)
    {
        _configId = configId; _create = create;
        Instances.Add(this);
    }

    public override void Tick()
    {
        if (_disposed) return;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var users = UserInstancesManager.Instance;
        if (!NetworkClient.isConnected || !NetworkClient.ready || plugin?.Sync.ClientCompatible != true || users == null)
        { Clear(); return; }
        ReleaseStale();
        _seen.Clear();
        foreach (var user in users.All())
        {
            var player = user != null ? user.playerEntity : null;
            if (!Held(player, _configId, out var idol)) continue;
            var id = player!.netId;
            _seen.Add(id);
            if (_faulted.TryGetValue(id, out var pointer) && pointer == idol!.Pointer) continue;
            try
            {
                if (_states.TryGetValue(id, out var state)
                    && (state.Player != player || state.Idol != idol || !state.Appearance.Matches(player)))
                { state.Appearance.Dispose(); _states.Remove(id); }
                if (!_states.ContainsKey(id))
                {
                    var appearance = _create(player);
                    if (appearance != null)
                    {
                        _states.Add(id, new State(player, idol!, appearance));
                        if (plugin.DebugTools.Enabled)
                            plugin.Log.LogInfo($"[Held appearance] config={_configId}, player={id}, idol={idol!.netId}.");
                    }
                }
            }
            catch (Exception error) { Fault(id, idol!, error); }
        }
        _removed.Clear();
        foreach (var id in _states.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) { _states[id].Appearance.Dispose(); _states.Remove(id); }
        _removed.Clear();
        foreach (var id in _faulted.Keys) if (!_seen.Contains(id)) _removed.Add(id);
        foreach (var id in _removed) _faulted.Remove(id);
    }

    internal static bool Held(PlayerEntity? player, ulong configId, out ScoreTool? idol)
    {
        idol = player?.InventoryNew?.InHand?.TryCast<ScoreTool>();
        return player != null && player.isActiveAndEnabled && player.CurrentHealthSync > 0
            && idol != null && idol.netId != 0 && idol.ItemOwner == player && idol.SoItem?.Id == configId;
    }

    private static void ReleaseStale()
    {
        // Release across types before any new owner/type captures shared body
        // or prop visibility. Registration/user order must not preserve our hide.
        foreach (var runtime in Instances)
        {
            runtime._removed.Clear();
            foreach (var pair in runtime._states)
                if (!Held(pair.Value.Player, runtime._configId, out var held) || held != pair.Value.Idol)
                    runtime._removed.Add(pair.Key);
            foreach (var id in runtime._removed)
            { runtime._states[id].Appearance.Dispose(); runtime._states.Remove(id); }
        }
    }

    internal static void RenderAll(bool disguisesOnly = false)
    {
        if (!NetworkClient.isConnected || !NetworkClient.ready
            || CalypsoExpansionSetPlugin.Instance?.Sync.ClientCompatible != true) { ClearAll(); return; }
        foreach (var runtime in Instances)
        {
            foreach (var pair in runtime._states)
            {
                if (runtime._faulted.ContainsKey(pair.Key)) continue;
                var state = pair.Value;
                if (disguisesOnly && state.Appearance is not Curses.PlayerDisguiseAppearance) continue;
                try
                {
                    if (Held(state.Player, runtime._configId, out var idol) && idol == state.Idol)
                        state.Appearance.Render();
                    else state.Appearance.Dispose();
                }
                catch (Exception error) { runtime.Fault(pair.Key, state.Idol, error, remove: false); }
            }
        }
        CalypsoExpansionSetPlugin.Instance?.Disguises?.Render();
    }

    internal static bool HasAppearance(PlayerEntity player)
    {
        foreach (var runtime in Instances)
            if (runtime._states.TryGetValue(player.netId, out var state) && state.Player == player
                && Held(player, runtime._configId, out var idol) && idol == state.Idol && state.Appearance.Matches(player)) return true;
        return false;
    }

    internal static void ClearAll()
    {
        foreach (var runtime in Instances) runtime.Clear();
    }

    private void Fault(uint id, ScoreTool idol, Exception error, bool remove = true)
    {
        if (_states.TryGetValue(id, out var state)) state.Appearance.Dispose();
        if (remove) _states.Remove(id);
        if (_faulted.TryGetValue(id, out var existing) && existing == idol.Pointer) return;
        _faulted[id] = idol.Pointer;
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Appearance disabled for player={id} until released: {error}");
    }

    private void Clear()
    {
        foreach (var state in _states.Values) state.Appearance.Dispose();
        _states.Clear(); _faulted.Clear(); _seen.Clear(); _removed.Clear();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Instances.Remove(this);
        Clear();
    }
}

internal sealed class StretchedPlayerAppearance : IPlayerAppearance
{
    private readonly PlayerEntity _player;
    private readonly GameObject _rig;
    private readonly VisualRigCopy _copy;
    private readonly Items.Item? _prop;
    private VisualRigCopy? _propCopy;

    internal static IPlayerAppearance? Create(PlayerEntity player, Vector3 stretch, float headScale)
    {
        if (player == _200_Dev.Entities.EntityManager.Me && !player.UseFreeCamWorldRig) return null;
        var curse = CalypsoExpansionSetPlugin.Instance?.Disguises;
        if (curse?.TryLook(player, out _) == true) return curse.CreateAppearance(player, stretch, headScale);
        var rig = player.PlayerWorldRigController;
        var root = player.WorldRigInstance;
        if (rig == null || root == null || rig.SkinnedMeshRenderer == null) return null;
        Transform? head = null;
        if (headScale != 1)
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (VisualRigCopy.IsSeparateItem(skin.transform, root.transform)) continue;
                foreach (var bone in skin.bones)
                    if (bone != null && bone.name.Contains("head", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("end", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("gear", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("ik", StringComparison.OrdinalIgnoreCase))
                    { head = bone; break; }
                if (head != null) break;
            }
            if (head == null) throw new InvalidOperationException("Cannot identify the visible character head bone.");
        }
        return new StretchedPlayerAppearance(player, root, stretch, head, headScale);
    }

    private StretchedPlayerAppearance(PlayerEntity player, GameObject rig, Vector3 stretch, Transform? head, float headScale)
    {
        _player = player; _rig = rig;
        _copy = new VisualRigCopy(rig.transform, stretch, head, headScale);
        try
        {
            // Wide bodies move the visible hands. Carry the held item's visual
            // geometry in the same copied space, leaving its native physics,
            // inventory and network transform untouched.
            _prop = stretch != Vector3.one ? player.InventoryNew?.InHand : null;
            if (_prop != null)
            {
                _propCopy = new VisualRigCopy(_prop.transform, Vector3.one);
                _propCopy.Root.SetParent(_copy.Root, false);
            }
        }
        catch { Dispose(); throw; }
    }
    public bool Matches(PlayerEntity player) => _copy.Alive && player == _player
        && CalypsoExpansionSetPlugin.Instance?.Disguises?.TryLook(player, out _) != true
        && (player != _200_Dev.Entities.EntityManager.Me || player.UseFreeCamWorldRig)
        && player.PlayerWorldRigController != null && player.WorldRigInstance == _rig;
    public void Render()
    {
        _copy.SyncPose();
        if (_propCopy == null || _prop == null) return;
        _propCopy.SyncPose();
        var origin = _rig.transform;
        _propCopy.Root.localPosition = origin.InverseTransformPoint(_prop.transform.position);
        _propCopy.Root.localRotation = Quaternion.Inverse(origin.rotation) * _prop.transform.rotation;
        var sourceScale = _prop.transform.lossyScale;
        var originScale = origin.lossyScale;
        if (Math.Abs(originScale.x) < 0.00001f || Math.Abs(originScale.y) < 0.00001f || Math.Abs(originScale.z) < 0.00001f)
            throw new InvalidOperationException("Visible rig has a degenerate scale.");
        _propCopy.Root.localScale = new Vector3(sourceScale.x / originScale.x, sourceScale.y / originScale.y, sourceScale.z / originScale.z);
    }
    public void Dispose() { _propCopy?.Dispose(); _propCopy = null; _copy.Dispose(); }
}

[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class HeldAppearanceDisconnectPatch
{
    [HarmonyPrefix] private static void Prefix() => HeldAppearanceRuntime.ClearAll();
}

[HarmonyPatch(typeof(RenderPipelineManager), "BeginContextRendering")]
internal static class HeldAppearanceRenderPatch
{
    [HarmonyPostfix] private static void Postfix() => HeldAppearanceRuntime.RenderAll();
    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null) Curses.PlayerDisguiseAppearance.EndRendering();
        return __exception;
    }
}
