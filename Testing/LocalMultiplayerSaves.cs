using System.Globalization;
using HarmonyLib;
using Mirror;
using _200_Dev.Utility;

namespace CalypsoExpansionSet.Testing;

internal static class LocalMultiplayerSaves
{
    internal const string Prefix = "本地多人测试_";

    internal static string NewName() => Prefix + DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture);

    internal static string[] GetSlots()
    {
        var slots = Save.GetSaveSlotNames();
        var names = new string[slots.Count];
        for (var i = 0; i < names.Length; i++) names[i] = slots[i];
        return names;
    }

    internal static string CopyForTest(string source)
    {
        if (!Save.TryGetSaveFilePath(source, out _, out var sourcePath)
            || !Save.TryReadEntries(sourcePath, out var story, out var endless, out _, readCollection: false)
            || (story == null && endless == null))
            throw new InvalidOperationException("The selected save has no readable run checkpoint. Refresh the save list and choose another save.");

        var slot = Save.GetAvailableSaveNameFromLobbyName(NewName());
        if (!slot.StartsWith(Prefix, StringComparison.Ordinal)
            || !Save.TryGetSaveFilePath(slot, out _, out var destinationPath))
            throw new InvalidOperationException("Could not allocate a named local multiplayer test save.");
        // Never load the source directly: native autosaves and clock consumption
        // must update the test copy, including when the source is a normal save.
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return slot;
    }
}

[HarmonyPatch(typeof(Save), "NextUnusedSlotName")]
internal static class LocalMultiplayerSaveNamePatch
{
    private static bool Prefix(ref string __result)
    {
        if (!LocalMultiplayer.IsActive || !NetworkServer.active) return true;
        // The native allocator is also used if a queued checkpoint fails to load.
        __result = Save.GetAvailableSaveNameFromLobbyName(LocalMultiplayerSaves.NewName());
        return false;
    }
}
