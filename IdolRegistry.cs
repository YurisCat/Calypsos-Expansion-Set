using BepInEx.Logging;
using Items;
using Mirror;
using UnityEngine;

namespace CalypsoExpansionSet;

internal sealed class RegisteredIdol
{
    internal IdolDefinition Definition { get; }
    internal SOScoreTool? Data { get; set; }
    internal IDisposable? Visuals { get; set; }
    internal IdolContentRuntime? Runtime { get; set; }
    internal RegisteredIdol(IdolDefinition definition) => Definition = definition;
}

internal sealed class IdolRegistry : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly List<RegisteredIdol> _entries = new();
    private readonly Dictionary<string, RegisteredIdol> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, RegisteredIdol> _byId = new();
    private readonly HashSet<uint> _assetIds = new();
    private SOItemLookup? _lookup;
    private GameObject? _prefabRoot;
    private bool _initializing;
    private bool _locked;
    private bool _disposed;

    internal IReadOnlyList<RegisteredIdol> Entries => _entries;
    internal bool Ready { get; private set; }
    internal ulong Manifest { get; private set; }

    internal IdolRegistry(ManualLogSource log) => _log = log;

    internal void Register(IdolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_locked || _disposed)
            throw new InvalidOperationException("Register content during plugin Load, before runtime initialization.");
        if (string.IsNullOrWhiteSpace(definition.Key) || definition.Key.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_'))
            || string.IsNullOrWhiteSpace(definition.DisplayName) || definition.ConfigId == 0
            || definition.AssetId == 0 || definition.TemplateId == 0 || definition.PoolTemplateId == 0 || definition.CreateEffect == null
            || definition.ShortDescription == null || definition.ShortDescription.Length > 80
            || definition.ShortDescription.Contains('\n') || definition.ShortDescription.Contains('\r')
            || definition.ContentVersion == 0 || !Enum.IsDefined(typeof(IdolEffectAuthority), definition.Authority)
            || definition.InitialWeight is { } weight && (!float.IsFinite(weight) || weight <= 0))
            throw new ArgumentException("Invalid idol key, IDs, factory, authority, version, or initial weight.");
        if (_byKey.ContainsKey(definition.Key) || _byId.ContainsKey(definition.ConfigId)
            || _assetIds.Contains(definition.AssetId))
            throw new InvalidOperationException($"Duplicate idol key/config ID/assetId: {definition.Key}.");
        var entry = new RegisteredIdol(definition);
        _entries.Add(entry);
        _byKey.Add(definition.Key, entry);
        _byId.Add(definition.ConfigId, entry);
        _assetIds.Add(definition.AssetId);
        _log.LogInfo($"[Definition] {definition.Key}: configId={definition.ConfigId}, assetId={definition.AssetId}, "
            + $"templateId={definition.TemplateId}, poolTemplateId={definition.PoolTemplateId ?? definition.TemplateId}, authority={definition.Authority}.");
    }

    internal bool TryGet(string key, out RegisteredIdol entry) => _byKey.TryGetValue(key, out entry!);
    internal bool TryGet(ulong id, out RegisteredIdol entry) => _byId.TryGetValue(id, out entry!);

    internal bool TryInitialize()
    {
        if (_disposed || _initializing)
            return Ready;
        _initializing = true;
        try
        {
            var lookup = SOItemLookup.Instance;
            if (lookup == null)
            {
                Ready = false;
                return false;
            }
            lookup.EnsureMaps();
            if (lookup.scoreTools == null || lookup._scoreById == null)
            {
                Ready = false;
                return false;
            }
            _locked = true;
            if (_lookup != null && _lookup.Pointer != lookup.Pointer) DetachLookup(_lookup);
            _lookup = lookup;
            foreach (var entry in _entries)
            {
                if (entry.Definition.PoolTemplateId is { } poolTemplateId
                    && (!SOItemLookup.TryGetScoreTool(poolTemplateId, out var poolTemplate) || poolTemplate == null))
                {
                    Ready = false;
                    return false;
                }
                if (entry.Data == null)
                {
                    if (!SOItemLookup.TryGetScoreTool(entry.Definition.TemplateId, out var template)
                        || template == null || template.prefab == null)
                        return false;
                    Create(entry, template, lookup);
                }
                AttachLookup(entry, lookup);
            }
            var firstReady = !Ready;
            if (firstReady)
            {
                Manifest = ComputeManifest();
                Ready = true;
            }
            EnsureNetworkPrefabs();
            if (firstReady) _log.LogInfo($"[Registry] ready: types={_entries.Count}, manifest={Manifest:X16}.");
            return true;
        }
        catch { Ready = false; throw; }
        finally { _initializing = false; }
    }

    private void Create(RegisteredIdol entry, SOScoreTool template, SOItemLookup lookup)
    {
        var definition = entry.Definition;
        if (lookup._scoreById.ContainsKey(definition.ConfigId))
            throw new InvalidOperationException($"Game config ID collision: {definition.ConfigId} ({definition.Key}).");
        foreach (var existing in lookup.scoreTools)
            if (existing != null && existing.Id == definition.ConfigId)
                throw new InvalidOperationException($"Game config list ID collision: {definition.ConfigId}.");
        if (NetworkClient.prefabs.ContainsKey(definition.AssetId)
            || NetworkClient.spawnHandlers.ContainsKey(definition.AssetId))
            throw new InvalidOperationException($"Network assetId collision: {definition.AssetId} ({definition.Key}).");

        if (_prefabRoot == null)
        {
            _prefabRoot = new GameObject("CalypsoExpansionSet Prefab Templates");
            _prefabRoot.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_prefabRoot);
        }
        var data = UnityEngine.Object.Instantiate(template);
        GameObject? prefab = null;
        IDisposable? visuals = null;
        IdolContentRuntime? runtime = null;
        try
        {
            data.name = $"CalypsoExpansionSet.{definition.Key}";
            data.hideFlags = HideFlags.DontSave;
            data.id = definition.ConfigId;
            definition.ConfigureData?.Invoke(data);
            // An inactive parent prevents Awake from seeing the template's data/identity.
            prefab = UnityEngine.Object.Instantiate(template.prefab, _prefabRoot.transform);
            prefab.name = data.name;
            var idol = prefab.GetComponent<ScoreTool>();
            var identity = prefab.GetComponent<NetworkIdentity>();
            if (idol == null || identity == null || data.id != definition.ConfigId)
                throw new InvalidOperationException($"Invalid ScoreTool prefab or changed config ID: {definition.Key}.");
            idol.gameplayElementData = data;
            identity.sceneId = 0;
            identity.assetId = definition.AssetId;
            definition.ConfigurePrefab?.Invoke(prefab);
            if (!string.IsNullOrEmpty(definition.ShortDescription))
            {
                // Only our clone opts in. Preserve native action/input/value UI;
                // the short description is rendered by the local hover controller.
                idol.DisplayInteractibleName = true;
                idol.DisplayDescription = false;
            }
            if (definition.CreateVisuals != null)
                visuals = definition.CreateVisuals(prefab)
                    ?? throw new InvalidOperationException($"CreateVisuals returned no resource lifetime: {definition.Key}.");
            if (prefab.GetComponent<ScoreTool>() == null || prefab.GetComponent<NetworkIdentity>() == null
                || data.id != definition.ConfigId || idol.gameplayElementData != data
                || identity.assetId != definition.AssetId || identity.sceneId != 0)
                throw new InvalidOperationException($"ConfigurePrefab changed required registration data: {definition.Key}.");
            // Native Instantiate(prefab) creates an active object outside the inactive template root.
            prefab.SetActive(true);
            data.prefab = prefab;
            if (definition.CreateRuntime != null)
                runtime = definition.CreateRuntime()
                    ?? throw new InvalidOperationException($"CreateRuntime returned no lifetime: {definition.Key}.");
            entry.Data = data;
            entry.Visuals = visuals;
            entry.Runtime = runtime;
            _log.LogInfo($"[Register] {definition.Key}: cloned config and prefab; assetId={identity.assetId}, "
                + $"scoreBasePoint={data.scoreBasePoint}, appearance={template.prefab.name}.");
        }
        catch
        {
            try { runtime?.Dispose(); }
            catch (Exception cleanup) { _log.LogError($"Runtime rollback failed for {definition.Key}: {cleanup}"); }
            if (prefab != null) UnityEngine.Object.Destroy(prefab);
            try { visuals?.Dispose(); }
            catch (Exception cleanup) { _log.LogError($"Visual rollback failed for {definition.Key}: {cleanup}"); }
            UnityEngine.Object.Destroy(data);
            throw;
        }
    }

    private static void AttachLookup(RegisteredIdol entry, SOItemLookup lookup)
    {
        var data = entry.Data!;
        if (lookup._scoreById.TryGetValue(data.Id, out var current))
        {
            if (current == null || current.Pointer != data.Pointer)
                throw new InvalidOperationException($"Another config occupies ID {data.Id}.");
        }
        else
        {
            lookup._scoreById.Add(data.Id, data);
            try
            {
                if (!lookup.scoreTools.Contains(data)) lookup.scoreTools.Add(data);
            }
            catch
            {
                lookup._scoreById.Remove(data.Id);
                throw;
            }
            return;
        }
        if (!lookup.scoreTools.Contains(data)) lookup.scoreTools.Add(data);
    }

    internal void EnsureNetworkPrefabs()
    {
        if (!Ready || _disposed)
            return;
        foreach (var entry in _entries)
        {
            var prefab = entry.Data!.prefab;
            if (NetworkClient.prefabs.TryGetValue(entry.Definition.AssetId, out var current))
            {
                if (current == null || current.Pointer != prefab.Pointer)
                    throw new InvalidOperationException($"Network assetId {entry.Definition.AssetId} was replaced.");
            }
            else
            {
                if (NetworkClient.spawnHandlers.ContainsKey(entry.Definition.AssetId))
                    throw new InvalidOperationException($"Custom spawn handler occupies assetId {entry.Definition.AssetId}.");
                NetworkClient.RegisterPrefab(prefab);
                if (!NetworkClient.prefabs.TryGetValue(entry.Definition.AssetId, out current)
                    || current == null || current.Pointer != prefab.Pointer)
                    throw new InvalidOperationException($"Mirror did not register assetId {entry.Definition.AssetId}.");
            }
        }
    }

    internal void ApplyInitialWeights()
    {
        if (!Ready || !NetworkServer.active)
            return;
        foreach (var entry in _entries)
            if (entry.Definition.InitialWeight is { } weight)
                ItemDropManager.SetIdolWeight(entry.Data!, weight);
    }

    private ulong ComputeManifest()
    {
        var hash = 14695981039346656037UL;
        foreach (var entry in _entries.OrderBy(entry => entry.Definition.ConfigId))
        {
            var definition = entry.Definition;
            var text = $"{definition.Key}|{definition.ConfigId:X16}|{definition.AssetId:X8}|"
                + $"{definition.TemplateId:X16}|{definition.PoolTemplateId ?? definition.TemplateId:X16}|"
                + $"{definition.ContentVersion:X8}|{(int)definition.Authority};";
            foreach (var character in text) { hash ^= character; hash *= 1099511628211UL; }
        }
        return hash;
    }

    internal void TickContentRuntimes()
    {
        if (_disposed) return;
        foreach (var entry in _entries) entry.Runtime?.Tick();
    }

    internal void StopContentRuntimes()
    {
        foreach (var entry in _entries)
        {
            try { entry.Runtime?.Dispose(); }
            catch (Exception cleanup) { _log.LogError($"Runtime cleanup failed for {entry.Definition.Key}: {cleanup}"); }
            entry.Runtime = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Ready = false;
        StopContentRuntimes();
        if (_lookup != null) DetachLookup(_lookup);
        foreach (var entry in _entries)
        {
            var data = entry.Data;
            if (data == null) continue;
            if (NetworkClient.prefabs.TryGetValue(entry.Definition.AssetId, out var prefab)
                && prefab != null && prefab.Pointer == data.prefab.Pointer)
                NetworkClient.prefabs.Remove(entry.Definition.AssetId);
            UnityEngine.Object.Destroy(data);
        }
        if (_prefabRoot != null) UnityEngine.Object.Destroy(_prefabRoot);
        _prefabRoot = null;
        foreach (var entry in _entries)
        {
            try { entry.Visuals?.Dispose(); }
            catch (Exception cleanup) { _log.LogError($"Visual cleanup failed for {entry.Definition.Key}: {cleanup}"); }
            entry.Visuals = null;
        }
    }

    private void DetachLookup(SOItemLookup lookup)
    {
        foreach (var entry in _entries)
        {
            var data = entry.Data;
            if (data == null) continue;
            lookup.scoreTools?.Remove(data);
            if (lookup._scoreById != null && lookup._scoreById.TryGetValue(data.Id, out var current)
                && current != null && current.Pointer == data.Pointer)
                lookup._scoreById.Remove(data.Id);
        }
    }
}
