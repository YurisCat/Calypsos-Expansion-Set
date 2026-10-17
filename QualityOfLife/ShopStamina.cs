using Entities.Player;
using EnvironmentElements;
using HarmonyLib;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.QualityOfLife;

internal sealed class ShopStamina : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private PlayerEntity? _player;
    private Room? _shop;
    private uint _visit;
    private IntPtr _connection;
    private bool _disposed, _failed, _inside;

    internal ShopStamina(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Tick() => Apply(EntityManager.Me);

    private bool Apply(PlayerEntity? player)
    {
        if (_disposed || _failed) return false;
        try
        {
            if (player != EntityManager.Me) return false;
            var connection = NetworkClient.connection?.Pointer ?? IntPtr.Zero;
            if (_connection != connection)
            {
                Reset();
                _connection = connection;
            }
            if (player == null || !player.isActiveAndEnabled
                || player.CurrentHealthSync <= 0 || player.PlayerMovement?.isOwned != true || _plugin.RuntimeFailed)
            { _inside = false; return false; }
            if (_player != player)
            { _player = player; _shop = null; _visit = 0; _inside = false; }
            var shop = RoomAreas.ActiveShop();
            var status = _plugin.RoomStatus;
            if (shop == null || !RoomAreas.Contains(shop, player.transform.position)
                || status == null || !status.InCurrentShop(player, shop, out var visit))
            { _inside = false; return false; }
            if (_shop != shop || _visit != visit)
            {
                var maximum = player.MaxStamina;
                if (!float.IsFinite(maximum) || maximum <= 0) { _inside = false; return false; }
                // Fill once per authoritative entry, including exhausted arrivals.
                // No shared settings, maximum, recovery flag or coroutine is owned.
                player.CliSetCurrentStamina(maximum);
                _shop = shop;
                _visit = visit;
            }
            if (!_inside && _plugin.DebugTools.Enabled)
                _plugin.Log.LogInfo($"[Shop stamina] active for player={player.netId}, room={shop.index}, visit={visit}.");
            _inside = true;
            return true;
        }
        catch (Exception error)
        {
            _failed = true;
            _inside = false;
            _plugin.Log.LogError($"Shop stamina disabled after an error; native stamina rules remain active: {error}");
            return false;
        }
    }

    internal void FilterCost(PlayerEntity player, ref float amount)
    {
        // Keep positive recovery and native method side effects. A stale server
        // grant cannot waive a cost after the owner has crossed the shop boundary.
        if (float.IsFinite(amount) && amount < 0 && Apply(player)) amount = 0;
    }

    internal void Reset()
    {
        _inside = false;
        _player = null;
        _shop = null;
        _visit = 0;
        _connection = IntPtr.Zero;
    }

    public void Dispose()
    {
        _disposed = true;
        Reset();
    }
}

[HarmonyPatch(typeof(PlayerEntity), nameof(PlayerEntity.CliUpdateStaminaContinuous))]
internal static class ShopContinuousStaminaPatch
{
    private static void Prefix(PlayerEntity __instance, ref float amount) =>
        CalypsoExpansionSetPlugin.Instance?.ShopStamina?.FilterCost(__instance, ref amount);
}
[HarmonyPatch(typeof(PlayerEntity), nameof(PlayerEntity.CliUpdateStaminaDiscrete))]
internal static class ShopDiscreteStaminaPatch
{
    private static void Prefix(PlayerEntity __instance, ref float amount) =>
        CalypsoExpansionSetPlugin.Instance?.ShopStamina?.FilterCost(__instance, ref amount);
}
