using CalypsoExpansionSet.Visuals;

namespace CalypsoExpansionSet.Idols;

internal sealed class OutlineIdol : HeldScreenEffect
{
    internal const ulong ConfigId = 0x4D4900000000000EUL;
    internal static IdolDefinition Definition => new(
        "outline", "描边雕像", ConfigId, 0x4D49000EU, 7331884207844971565UL,
        IdolEffectAuthority.LocalPresentation, () => new OutlineIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "黑底白线，轮廓浮现",
        ContentVersion = 3,
        CreateVisuals = prefab => ScreenEffectResources.Create(prefab, "handdrawn_outline")
    };
    private OutlineIdol() : base(ConfigId, IdolScreenStyle.Outline) { }
}
