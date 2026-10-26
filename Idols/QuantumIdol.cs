using CalypsoExpansionSet.Lobby;
using CalypsoExpansionSet.Visuals;
using UnityEngine;

namespace CalypsoExpansionSet.Idols;

internal sealed class QuantumIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000011UL;
    internal static IdolDefinition Definition => new(
        "quantum", "游移雕像", ConfigId, 0x4D490011U,
        7331884207844971565UL, IdolEffectAuthority.ServerGameplay, () => new QuantumIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "目不转睛，方能安心",
        CreateVisuals = prefab =>
        {
            IdolEyeOverlay.Validate(prefab.GetComponent<ScoreTool>());
            return StickyNoteVisuals.Create(prefab, "handdrawn_quantum");
        },
        CreateRuntime = () => new QuantumIdolRuntime(),
        SharedVisual = new IdolSharedVisualDefinition(IdolVisualAuthority.Server,
            idol => new IdolEyeProgressVisual(idol, "Calypso Quantum Eyes",
                value => value >= 0.5f ? PlayerPresenceNames.InsideColor : Color.black)) { ShowWhenDropped = true },
        ContentVersion = 8
    };

    // The content runtime owns observation while dropped; carrying suspends it.
    public override void Tick(HeldIdolContext context) { }
}
