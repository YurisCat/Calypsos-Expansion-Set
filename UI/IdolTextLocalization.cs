namespace CalypsoExpansionSet.UI;

// Presentation-only translations. Match both the stable config ID and identity
// so an external definition with a similar key keeps its own supplied text.
internal static class IdolTextLocalization
{
    private static readonly Dictionary<ulong, IdolHoverText> English = new()
    {
        [7331884207844971565UL] = new("original_score", "Plain Idol",
            "No hidden hex, no secret bite; just bear this burden home tonight.", false),
        [14115087848621455930UL] = new("original_wandering", "Wandering Idol",
            "It sprouts two legs and flees your hold; give chase before the trail grows cold.", false),
        [5804517731052606251UL] = new("original_musical", "Red Light Idol",
            "While music plays, step light and spry; when red lights glow, move and you cry.", false),
        [13601636613369722279UL] = new("original_slippery", "Skating Idol",
            "Your feet will slip, your steps will slide; beware the cliff beside your stride.", false),
        [16473980543408359611UL] = new("original_reverse_inputs", "Reverse Idol",
            "Your steps and gaze defy your will; reverse your instincts, test your skill.", false),
        [16910182908390641787UL] = new("original_inverted_view", "Mirror Idol",
            "The mirrored world will twist your view; beware which way is right for you.", false),
        [15659945856436154005UL] = new("original_blind", "Blinding Idol",
            "A shroud of black conceals the way; set down your load to spy the fray.", false),
        [12142498195737046383UL] = new("original_vanishing", "Crystal Idol",
            "Far friends and foes escape your sight; draw near to bring them back to light.", false),
        [8443964997203258129UL] = new("original_miniature", "Shrinking Idol",
            "Your body shrinks, your pace runs slow; mind every ledge before you go.", false),
        [5678489519887607110UL] = new("original_flamethrower", "Firebreathing Idol",
            "Its turning head will spit a flame; both friend and foe can burn the same.", false),
        [6217483385191454636UL] = new("original_lightning", "Lightning Idol",
            "The warning marks where bolts will bite; step clear before the flash of light.", false),
        [970001822794563052UL] = new("original_dice_thrower", "Dice-Spitting Idol",
            "It spits out dice that bounce and roll; their wicked tricks may take their toll.", false),
        [12728186048785612789UL] = new("original_puppet", "Puppet Idol",
            "Another's hands direct your stride; agree which way before the ride.", false),
        [16161024687220126744UL] = new("original_camera", "Camera Idol",
            "A friend's own eyes become your view; ask them to keep a watch on you.", false),
        [6096484020692488796UL] = new("original_prankster", "Teleporting Idol",
            "One sudden blink, you're far away; find where you are before you stray.", false),
        [13539466722732671913UL] = new("original_heavy", "Heavy Idol",
            "No secret curse to make you quake; just heavy steps with each you take.", false),
        [3611313213204598715UL] = new("original_hallucination", "Hallucination Idol",
            "False monsters mingle with the true; ask friends to check what stalks your view.", false),
        [14189525892065481450UL] = new("original_meduza", "Gazing Idol",
            "A staring friend will freeze in place; warn them to turn from its cold face.", false),
        [10036870583278101750UL] = new("original_linked", "Rope Idol",
            "A rope unites your fates as one; agree your path before you run.", false),
        [887832889495486297UL] = new("original_final", "",
            "Take up the last and head for home; no time remains for you to roam.", false),
        [17513984537194232416UL] = new("original_missing_piece", "",
            "A missing piece, a puzzling plight; heed the quest to set things right.", false),

        [0x4D49000000000001UL] = new("jumping", "Bouncing Idol",
            "Your feet touch down, then up you bound; beware the ears that catch the sound.", true),
        [0x4D49000000000003UL] = new("near_view", "Zoom Idol",
            "The world draws close, the edges hide; look down before your next bold stride.", true),
        [0x4D49000000000004UL] = new("voice", "Voice Idol",
            "Speak aloud to spur your stride; hold your tongue and steps subside.", true),
        [0x4D49000000000008UL] = new("upside_down", "Upside-Down Idol",
            "The floor swings high, the ceiling low; mind which way your glances go.", true),
        [0x4D49000000000005UL] = new("duck_step", "Duckstep Idol",
            "Your waddling steps may quack a tune; those hunting ears may find you soon.", true),
        [0x4D49000000000006UL] = new("extreme_speed", "Speed Idol",
            "With racing feet you dash and fly; let go, and still the world goes by.", true),
        [0x4D49000000000007UL] = new("delayed_move", "Sluggish Idol",
            "You bid your feet to turn or stay; they heed your wish a beat away.", true),
        [0x4D49000000000009UL] = new("relay", "Relay Idol",
            "The longer held, the slower you; pass on the weight to start anew.", true),
        [0x4D4900000000000AUL] = new("suspicious", "Paranoia Idol",
            "False cries resound where none appear; yet living hunters still may hear.", true),
        [0x4D4900000000000BUL] = new("wide_body", "Widebody Idol",
            "Your shape grows wide, your view sinks low; a flattened friend puts on a show.", true),
        // Reserved text only: localization must never enable retired content.
        [0x4D4900000000000CUL] = new("monster_disguise", "Monster Disguise Idol",
            "A monster's mask, a borrowed face; no borrowed powers join the chase.", true),
        [0x4D4900000000000DUL] = new("mosaic", "Pixel Idol",
            "The world breaks down to squares of light; a blocky veil now fills your sight.", true),
        [0x4D4900000000000EUL] = new("outline", "Outline Idol",
            "On blackest night, white edges glow; no hidden things these outlines show.", true),
        [0x4D4900000000000FUL] = new("big_head_fisheye", "Big Head Idol",
            "Your head swells large, the edges bend; a fish-eyed world awaits you, friend.", true)
    };

    internal static bool IsChinese(string? code, string? language)
    {
        // The game's I2 codes include regional Chinese variants. Name fallback
        // is only for startup/sources without a code; OS culture is irrelevant.
        if (!string.IsNullOrEmpty(code))
            return code.Equals("zh", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("zh_", StringComparison.OrdinalIgnoreCase);
        return language != null && (language.Equals("Chinese", StringComparison.OrdinalIgnoreCase)
            || language.StartsWith("Chinese (", StringComparison.OrdinalIgnoreCase)
            || language.StartsWith("Chinese/", StringComparison.OrdinalIgnoreCase));
    }

    internal static string GenericName(bool chinese) =>
        chinese ? "卡里普索拓展包雕像" : "Calypso's Expansion Set Idol";

    internal static IdolHoverText Resolve(ulong configId, IdolHoverText original, bool chinese) =>
        !chinese && English.TryGetValue(configId, out var translated)
            && translated.Key == original.Key && translated.IsMod == original.IsMod
            ? translated : original;
}
