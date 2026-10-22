using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using _200_Dev.EnvironmentElements.Furnitures;
using _200_Dev.Utility;

namespace CalypsoExpansionSet.QualityOfLife;

internal sealed class PermanentOldClock
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly ConfigEntry<bool> _enabled;
    private int _consuming;
    private bool _failed;

    internal PermanentOldClock(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _enabled = plugin.Config.Bind("Gameplay", "PermanentOldClock", false,
            "购买的老旧挂钟可反复使用，仍需有效存档；新游戏需重新购买。仅主机设置生效，修改后重启。\n"
            + "/ Makes a purchased Old Clock reusable; a valid save is required. Buy it again in a New Game. Host setting only; restart after changes.");
    }

    internal bool BeginConsumption()
    {
        if (!_enabled.Value || _failed || _plugin.RuntimeFailed || !NetworkServer.active) return false;
        try
        {
            var manager = GameRoomFurnituresManager.Instance;
            if (manager == null || !manager.IsFurnitureActive(EGameRoomFurniture.OLD_CLOCK)) return false;
            _consuming++;
            return true;
        }
        catch (Exception error) { Fail(error); return false; }
    }

    internal void EndConsumption()
    {
        if (_consuming > 0) _consuming--;
    }

    internal void PreserveCheckpoint(SaveData? saveData)
    {
        if (_consuming == 0 || _failed || !NetworkServer.active) return;
        try
        {
            if (Save.PendingLoadReason != Save.EPendingLoadReason.LastChance) return;
            // Native consumption has selected the checkpoint, queued the rewind
            // and applied the purchase charge. Put back only the clock before
            // native metadata generation and serialization.
            var furniture = saveData?.furnitures?.furnitures;
            if (furniture != null && !furniture.Contains(EGameRoomFurniture.OLD_CLOCK))
                furniture.Add(EGameRoomFurniture.OLD_CLOCK);
        }
        catch (Exception error) { Fail(error); }
    }

    private void Fail(Exception error)
    {
        if (_failed) return;
        _failed = true;
        _plugin.Log.LogError($"Permanent Old Clock disabled until restart after an error; vanilla save handling continues.\n{error}");
    }
}

[HarmonyPatch(typeof(Save), nameof(Save.ConsumeLastChance))]
internal static class PermanentOldClockConsumePatch
{
    private static void Prefix(out PermanentOldClock? __state)
    {
        var clock = CalypsoExpansionSetPlugin.Instance?.PermanentClock;
        __state = clock?.BeginConsumption() == true ? clock : null;
    }

    private static Exception? Finalizer(Exception? __exception, PermanentOldClock? __state)
    {
        __state?.EndConsumption();
        return __exception;
    }
}

[HarmonyPatch(typeof(Save), "BuildMetadata")]
internal static class PermanentOldClockCheckpointPatch
{
    private static void Prefix(SaveData? saveData) =>
        CalypsoExpansionSetPlugin.Instance?.PermanentClock?.PreserveCheckpoint(saveData);
}
