using CalypsoExpansionSet.Visuals;
using UnityEngine;

namespace CalypsoExpansionSet.Idols;

internal sealed class BigHeadFisheyeIdol : HeldScreenEffect
{
    internal const ulong ConfigId = 0x4D4900000000000FUL;
    internal static IdolDefinition Definition => new(
        "big_head_fisheye", "大头雕像", ConfigId, 0x4D49000FU, 7331884207844971565UL,
        IdolEffectAuthority.LocalPresentation, () => new BigHeadFisheyeIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "大头大头，凹眼凸头",
        ContentVersion = 2,
        CreateVisuals = prefab => ScreenEffectResources.Create(prefab, "todo_big_head_fisheye"),
        CreateRuntime = () => new HeldAppearanceRuntime(ConfigId,
            player => StretchedPlayerAppearance.Create(player, Vector3.one, 2f))
    };
    private BigHeadFisheyeIdol() : base(ConfigId, IdolScreenStyle.Fisheye) { }
}
