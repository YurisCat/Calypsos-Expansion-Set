using CalypsoExpansionSet.Visuals;

namespace CalypsoExpansionSet.Idols;

internal sealed class MonsterDisguiseIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D4900000000000CUL;
    internal static IdolDefinition Definition => new(
        "monster_disguise", "怪相雕像", ConfigId, 0x4D49000CU, 7331884207844971565UL,
        IdolEffectAuthority.ServerGameplay, () => new MonsterDisguiseIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "怪物模样，队友着相",
        ContentVersion = 14,
        HideHintsWhileHeld = true,
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "handdrawn_monster_disguise"),
        CreateRuntime = () => new MonsterDisguiseRuntime()
    };
    private HeldIdolContext _context;
    public override void Start(HeldIdolContext context) { _context = context; MonsterDisguiseRuntime.Choose(context); }
    public override void Tick(HeldIdolContext context) { _context = context; MonsterDisguiseRuntime.Choose(context); }
    public override void Dispose() { MonsterDisguiseRuntime.Release(_context); _context = default; }
}
