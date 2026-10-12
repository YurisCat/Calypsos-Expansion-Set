using Entities.Player;
using Items;
using UnityEngine;

namespace MoreIdols;

public enum IdolEffectAuthority
{
    LocalPresentation,
    OwnerNativeGameplay,
    ServerGameplay
}

public readonly record struct HeldIdolContext(ScoreTool Idol, PlayerEntity Holder, bool GameplayReady);

public abstract class HeldIdolEffect : IDisposable
{
    public virtual void Start(HeldIdolContext context) { }
    public abstract void Tick(HeldIdolContext context);
    public virtual void Dispose() { }
}

public sealed record IdolDefinition(
    string Key,
    string DisplayName,
    ulong ConfigId,
    uint AssetId,
    ulong TemplateId,
    IdolEffectAuthority Authority,
    Func<HeldIdolEffect> CreateEffect)
{
    public uint ContentVersion { get; init; } = 1;
    public float? InitialWeight { get; init; }
    public Action<SOScoreTool>? ConfigureData { get; init; }
    public Action<GameObject>? ConfigurePrefab { get; init; }
}

public static class MoreIdolsApi
{
    public static void Register(IdolDefinition definition)
    {
        var plugin = MoreIdolsPlugin.Instance
            ?? throw new InvalidOperationException("Load More Idols before registering content; add a BepInDependency.");
        plugin.Registry.Register(definition);
    }
}
