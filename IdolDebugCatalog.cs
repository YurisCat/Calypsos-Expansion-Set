using Items;
using UnityEngine;

namespace CalypsoExpansionSet;

internal sealed class IdolDebugCatalog
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Dictionary<ulong, ScoreTool> _instances = new();
    internal static bool SpawningOriginal { get; private set; }
    internal int InstanceCount => _instances.Values.Count(idol => idol != null);

    internal IdolDebugCatalog(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal List<(string Key, SOScoreTool Data)> GetOriginals()
    {
        var result = new List<(string Key, SOScoreTool Data)>();
        var lookup = SOItemLookup.Instance;
        if (lookup == null || lookup.scoreTools == null) return result;
        var ids = new HashSet<ulong>();
        foreach (var data in lookup.scoreTools)
        {
            if (data == null || data.Id == 0 || _plugin.Registry.TryGet(data.Id, out _) || !ids.Add(data.Id)
                || data.prefab == null || data.prefab.GetComponent<ScoreTool>() == null
                || data.prefab.GetComponent<MissingPieceTool>() != null
                || data.prefab.GetComponent<Entities.Monster.MonsterEntity>() != null) continue;
            var name = data.name;
            if (name.StartsWith("Data-Treasure-", StringComparison.OrdinalIgnoreCase)) name = name[14..];
            else if (name.StartsWith("Data-", StringComparison.OrdinalIgnoreCase)) name = name[5..];
            var key = "original:" + new string(name.ToLowerInvariant().Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray());
            result.Add((key, data));
        }
        // Resource names are convenient aliases; IDs keep duplicate names unambiguous.
        var duplicates = result.GroupBy(entry => entry.Key).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < result.Count; index++)
            if (duplicates.Contains(result[index].Key) || result[index].Key == "original:")
                result[index] = ($"original:{result[index].Data.Id}", result[index].Data);
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        return result;
    }

    internal bool TryGet(string key, out SOScoreTool? data)
    {
        data = null;
        if (!key.StartsWith("original:", StringComparison.Ordinal))
        {
            if (_plugin.Registry.TryGet(key, out _)) return false;
            key = "original:" + key;
        }
        foreach (var entry in GetOriginals())
            if (entry.Key == key || key == $"original:{entry.Data.Id}") { data = entry.Data; return true; }
        return false;
    }

    internal bool HasInstance(ulong id) => _instances.TryGetValue(id, out var idol) && idol != null;
    internal void Track(SOScoreTool data, ScoreTool idol) => _instances[data.Id] = idol;

    internal ScoreTool? Spawn(ItemDropManager manager, SOScoreTool data, Vector3 position)
    {
        var previous = SpawningOriginal;
        SpawningOriginal = true;
        try { return _plugin.SpawnDebugIdol(manager, data, position); }
        finally { SpawningOriginal = previous; }
    }
}
