using Entities.Player;
using Items;
using UnityEngine;

namespace CalypsoExpansionSet;

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

// Optional per-type service for persistent, authoritative state and observer
// presentation. Unlike HeldIdolEffect, this lifetime survives dropping an item.
public abstract class IdolContentRuntime : IDisposable
{
    public abstract void Tick();
    public abstract void Dispose();
}

// A normalized presentation value, never a client-authoritative gameplay value.
public enum IdolVisualAuthority { Server, Holder }

public interface IIdolStateVisual : IDisposable
{
    void Apply(float value);
    void Tick(float deltaTime);
}

public sealed record IdolSharedVisualDefinition(
    IdolVisualAuthority Authority,
    Func<ScoreTool, IIdolStateVisual> Create)
{
    public bool ShowWhenDropped { get; init; }
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
    public ulong? PoolTemplateId { get; init; }
    public float? InitialWeight { get; init; }
    public Action<SOScoreTool>? ConfigureData { get; init; }
    public Action<GameObject>? ConfigurePrefab { get; init; }
    // Called on the owned prefab. Return the visual resources' lifetime; clean up
    // locally if construction throws. The registry disposes it on rollback/unload.
    public Func<GameObject, IDisposable>? CreateVisuals { get; init; }
    public Func<IdolContentRuntime>? CreateRuntime { get; init; }
    // Opt in to a local hover name and a short HUD description. No network UI messages.
    public string ShortDescription { get; init; } = "";
    // The callback can change owned eyes, other parts or material properties.
    // The shared service synchronizes one finite value in [0,1] per instance.
    public IdolSharedVisualDefinition? SharedVisual { get; init; }
}

public static class CalypsoExpansionSetApi
{
    public static void PublishVisualState(ScoreTool idol, float value) =>
        CalypsoExpansionSetPlugin.Instance?.VisualStates?.Publish(idol, value);

    public static void Register(IdolDefinition definition)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance
            ?? throw new InvalidOperationException("Load Calypso's Expansion Set before registering content; add a BepInDependency.");
        plugin.Registry.Register(definition);
    }
}
