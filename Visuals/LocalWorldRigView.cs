using Entities.Player;
using Entities.Player.Behaviours;
using HarmonyLib;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.Visuals;

// The game's removed free-camera flag no longer represents third-person views.
// Track only the temporary presentation owned by our preview/inspection tools.
internal sealed class LocalWorldRigView : IDisposable
{
    private static LocalWorldRigView? _active;
    private static bool _writing;
    private PlayerEntity? _player;
    private GameObject? _rig;
    private PlayerRigConfig? _localRig, _worldRig;

    internal static bool IsVisible(PlayerEntity player) => _active?._player == player
        && player != null && player.WorldRigInstance == _active._rig
        && player.PlayerLocalRigController == _active._localRig && player.PlayerWorldRigController == _active._worldRig;

    internal bool TryShow(PlayerEntity player)
    {
        if (_active != null || player == null || player != EntityManager.Me || player.WorldRigInstance == null
            || player._clothChangeItemsOnWorldRig
            || CameraManager.Instance?.LocalAnimatorState == CameraManager.ECameraAnimatorState.CHARACTER_SWAP)
            return false;
        _player = player;
        _rig = player.WorldRigInstance;
        _localRig = player.PlayerLocalRigController;
        _worldRig = player.PlayerWorldRigController;
        _active = this;
        Write(player, true);
        return true;
    }

    private static void Write(PlayerEntity player, bool display)
    {
        _writing = true;
        try { player.CliDisplayThirdPersonRig(display); }
        finally { _writing = false; }
    }

    internal static void NativeVisibilityChanging(PlayerRigConfig rig)
    {
        // Clothing, character swaps and other native views take ownership.
        // A later preview cleanup must not undo their presentation.
        var player = _active?._player;
        if (!_writing && player != null
            && (rig == player.PlayerLocalRigController || rig == player.PlayerWorldRigController))
            _active = null;
    }

    public void Dispose()
    {
        var player = _player;
        var owns = _active == this;
        if (owns) _active = null;
        _player = null;
        var rig = _rig;
        _rig = null;
        var sameRigs = player != null && player.PlayerLocalRigController == _localRig && player.PlayerWorldRigController == _worldRig;
        _localRig = _worldRig = null;
        if (owns && player != null && player == EntityManager.Me && player.WorldRigInstance == rig
            && sameRigs
            && !player._clothChangeItemsOnWorldRig
            && CameraManager.Instance?.LocalAnimatorState != CameraManager.ECameraAnimatorState.CHARACTER_SWAP)
            Write(player, false);
    }
}

[HarmonyPatch(typeof(PlayerEntity), "DisplayThirdPersonRigState")]
internal static class LocalWorldRigVisibilityPatch
{
    private static void Prefix(PlayerRigConfig rig) => LocalWorldRigView.NativeVisibilityChanging(rig);
}
