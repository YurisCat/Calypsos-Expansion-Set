using CalypsoExpansionSet.Visuals;

namespace CalypsoExpansionSet.Idols;

internal sealed class MosaicIdol : HeldScreenEffect
{
    internal const ulong ConfigId = 0x4D4900000000000DUL;
    internal static IdolDefinition Definition => new(
        "mosaic", "马赛克雕像", ConfigId, 0x4D49000DU, 7331884207844971565UL,
        IdolEffectAuthority.LocalPresentation, () => new MosaicIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "方格景色，麦块福特",
        ContentVersion = 2,
        CreateVisuals = prefab => ScreenEffectResources.Create(prefab, "todo_mosaic")
    };
    private MosaicIdol() : base(ConfigId, IdolScreenStyle.Mosaic) { }
}
