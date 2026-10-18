using CalypsoExpansionSet.Visuals;
using Entities.Player;
using Entities.Player.Behaviours;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using User;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Curses;

internal sealed class PlayerDisguiseAppearance : IPlayerAppearance
{
    private static readonly HashSet<PlayerDisguiseAppearance> Rendering = new();
    private readonly DisguiseCurse _curse;
    private readonly DisguiseLook _look;
    private readonly GameObject _rig;
    private readonly SkinnedMeshRenderer _body;
    private readonly VisualRigCopy _copy;
    private readonly Renderer[] _hidden = Array.Empty<Renderer>();
    private readonly bool[] _priorOff = Array.Empty<bool>();
    private readonly (GameObject Object, bool Active)[] _headgear;
    private readonly int _layer;
    private readonly TextMeshProUGUI? _nickname;
    private readonly DisguiseHeldVisuals? _props;
    private readonly UserInstancesManager.EPlayerCharacter _character;
    private string? _writtenNick, _priorNick;
    private bool _disposed, _rendering;
    private int _hiddenCount;
    internal PlayerEntity Player { get; }

    internal static PlayerDisguiseAppearance? Create(DisguiseCurse curse, PlayerEntity player, DisguiseLook look,
        Vector3 stretch, float headScale)
    {
        if (player == EntityManager.Me && !player.UseFreeCamWorldRig) return null;
        var rig = player.WorldRigInstance;
        if (rig == null || player.PlayerWorldRigController?.SkinnedMeshRenderer == null) return null;
        var infos = UserInstancesManager.Instance?.PlayerCharacterInfos;
        if (infos == null) return null;
        GameObject? reference = null, template = null;
        foreach (var info in infos)
        {
            if (info.playerCharacter == player.PlayerCharacter) reference = info.worldRigPrefab;
            if ((int)info.playerCharacter == look.Character) template = info.worldRigPrefab;
        }
        return reference != null && template != null
            ? new PlayerDisguiseAppearance(curse, player, rig, template, reference, look, stretch, headScale) : null;
    }

    private PlayerDisguiseAppearance(DisguiseCurse curse, PlayerEntity player, GameObject rig, GameObject template, GameObject reference,
        DisguiseLook look, Vector3 stretch, float headScale)
    {
        _curse = curse; Player = player; _rig = rig; _look = look; _character = player.PlayerCharacter;
        _nickname = player.NickWorldSpaceUI?.nickText;
        _body = player.PlayerWorldRigController.SkinnedMeshRenderer;
        _layer = _body.gameObject.layer;
        var config = template.GetComponent<PlayerRigConfigWorld>();
        Transform? head = null;
        if (headScale != 1f)
            foreach (var skin in template.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in skin.bones)
                    if (bone != null && bone.name.Contains("head", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("end", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("gear", StringComparison.OrdinalIgnoreCase)
                        && !bone.name.Contains("ik", StringComparison.OrdinalIgnoreCase)) { head = bone; break; }
                if (head != null) break;
            }
        if (headScale != 1f && head == null) throw new InvalidOperationException("Disguise head bone is unavailable.");
        _copy = new VisualRigCopy(template.transform, stretch, head, headScale, hideSource: false, suppressProximityDither: true);
        try
        {
            _copy.BindPoseTo(rig.transform, reference.transform);
            foreach (var transform in _copy.Root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = _layer;
            if (config?.SkinnedMeshRenderer != null && config._clothConfig != null)
            {
                var body = _copy.CopiedRenderer(config.SkinnedMeshRenderer);
                if (body != null)
                {
                    var cloth = config._clothConfig.GetClothMaterialsByColorAndCharacter(
                        (SOClothConfig.ClothColor)look.Cloth, (UserInstancesManager.EPlayerCharacter)look.Character);
                    if (cloth != null)
                    {
                        // Native ChangeCloth builds [cloth, ...base skin/hair materials].
                        var originals = config._baseBodyMaterial;
                        var materials = originals != null ? new Material[originals.Count + 1] : body.sharedMaterials.ToArray();
                        if (materials.Length == 0) throw new InvalidOperationException("Disguise body has no material slots.");
                        materials[0] = cloth;
                        if (originals != null)
                            for (var index = 0; index < originals.Count; index++) materials[index + 1] = originals[index];
                        body.sharedMaterials = materials;
                    }
                }
            }
            var gears = new List<(GameObject, bool)>();
            var data = config?.HeadGearGearConfig?.headConfigData;
            if (data != null)
                foreach (var gear in data)
                {
                    if (gear == null || gear.headGearPrefab == null) continue;
                    var copied = _copy.CopiedTransform(gear.headGearPrefab.transform);
                    if (copied != null) gears.Add((copied.gameObject, (look.Headgear & (1 << (int)gear.headConfigType)) != 0));
                }
            _headgear = gears.ToArray();
            _props = new DisguiseHeldVisuals(player, config, _copy);
            _hidden = rig.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => !_props.Owns(renderer) && !VisualRigCopy.IsSeparateItem(renderer.transform, rig.transform)
                    && (renderer.TryCast<MeshRenderer>() != null || renderer.TryCast<SkinnedMeshRenderer>() != null)).ToArray();
            _priorOff = new bool[_hidden.Length];
            _copy.Root.gameObject.SetActive(false);
        }
        catch { Dispose(); throw; }
    }

    public bool Matches(PlayerEntity player) => !_disposed && _copy.Alive && _rig != null && player != null && player == Player
        && player.WorldRigInstance == _rig && player.PlayerWorldRigController != null
        && player.PlayerWorldRigController.SkinnedMeshRenderer == _body
        && player.PlayerCharacter == _character
        && player.NickWorldSpaceUI?.nickText == _nickname
        && (player != EntityManager.Me || player.UseFreeCamWorldRig)
        && (_props == null || _props.Matches(player))
        && _curse.TryLook(player, out var look) && look == _look;

    internal void RenderNickname()
    {
        if (!Matches(Player) || Player.CurrentHealthSync <= 0) { Dispose(); return; }
        if (_nickname == null) return;
        if (_writtenNick == null || _nickname.text != _writtenNick) _priorNick = _nickname.text;
        _writtenNick = _look.Nick;
        if (_nickname.text != _writtenNick) _nickname.text = _writtenNick;
    }

    public void Render()
    {
        if (!Matches(Player) || Player.CurrentHealthSync <= 0) { Dispose(); return; }
        RestoreHidden();
        if (!_body.enabled || !_body.gameObject.activeInHierarchy || _body.forceRenderingOff)
        { HideCopy(); return; }
        _copy.SyncPose();
        foreach (var gear in _headgear) if (gear.Object != null) gear.Object.SetActive(gear.Active);
        _copy.Root.gameObject.SetActive(_rig.activeInHierarchy);
        _rendering = true;
        Rendering.Add(this);
        _hiddenCount = 0;
        for (var index = 0; index < _hidden.Length; index++)
            if (_hidden[index] != null)
            {
                _priorOff[index] = _hidden[index].forceRenderingOff;
                _hiddenCount = index + 1;
                _hidden[index].forceRenderingOff = true;
            }
        _props?.Render();
    }

    internal void HideCopy()
    {
        RestoreHidden();
        _props?.Hide();
        if (!_disposed) _copy.Root.gameObject.SetActive(false);
    }

    private void RestoreHidden()
    {
        _props?.Restore();
        if (!_rendering) return;
        for (var index = 0; index < _hiddenCount; index++)
            if (_hidden[index] != null && _hidden[index].forceRenderingOff) _hidden[index].forceRenderingOff = _priorOff[index];
        _hiddenCount = 0;
        _rendering = false;
    }

    internal static void EndRendering()
    {
        foreach (var view in Rendering) view.HideCopy();
        Rendering.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreHidden();
        Rendering.Remove(this);
        if (_nickname != null && _writtenNick != null && _nickname.text == _writtenNick)
            _nickname.text = Player != null && Player.NickWorldSpaceUI?.nickText == _nickname ? Player.PlayerNick : _priorNick ?? "";
        _writtenNick = null;
        _props?.Dispose();
        _copy.Dispose();
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "EndContextRendering")]
internal static class DisguiseEndRenderPatch
{
    private static void Postfix() => PlayerDisguiseAppearance.EndRendering();
    private static Exception? Finalizer(Exception? __exception)
    {
        PlayerDisguiseAppearance.EndRendering();
        return __exception;
    }
}

[HarmonyPatch(typeof(RenderPipelineManager), "DoRenderLoop_Internal")]
internal static class DisguiseRenderLoopFinallyPatch
{
    private static Exception? Finalizer(Exception? __exception)
    {
        PlayerDisguiseAppearance.EndRendering();
        return __exception;
    }
}
