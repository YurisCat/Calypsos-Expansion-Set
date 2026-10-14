namespace CalypsoExpansionSet.UI;

internal readonly record struct IdolHoverText(
    string Key, string DisplayName, string ShortDescription, bool IsMod);

// Match the item's own SOScoreTool ID, never its template, skin or localized name.
// These are local UI texts; original configs and gameplay remain owned by the game.
internal static class OriginalIdolAdvice
{
    private static readonly Dictionary<ulong, IdolHoverText> Texts = new()
    {
        [7331884207844971565UL] = new("original_score", "普通雕像", "普普通通，搬运轻松", false),
        [14115087848621455930UL] = new("original_wandering", "游荡雕像", "雕像长腿，不追后悔", false),
        [5804517731052606251UL] = new("original_musical", "红灯雕像", "红头别走，小心扎手", false),
        [13601636613369722279UL] = new("original_slippery", "滑冰雕像", "打呲溜滑，远离悬崖", false),
        [16473980543408359611UL] = new("original_reverse_inputs", "反向雕像", "走左往右，按前马后", false),
        [16910182908390641787UL] = new("original_inverted_view", "镜像雕像", "左右翻转，方向难判", false),
        [15659945856436154005UL] = new("original_blind", "致盲雕像", "迷雾环绕，撞怪吓尿", false),
        [12142498195737046383UL] = new("original_vanishing", "水晶雕像", "越近越显，越远越险", false),
        [8443964997203258129UL] = new("original_miniature", "变小雕像", "叽里呱啦，小小一趴", false),
        [5678489519887607110UL] = new("original_flamethrower", "喷火雕像", "转头喷火，不分敌我", false),
        [6217483385191454636UL] = new("original_lightning", "雷电雕像", "天雷滚滚，化为齑粉", false),
        [970001822794563052UL] = new("original_dice_thrower", "吐骰雕像", "烂骰乱吐，没啥好处", false),
        [12728186048785612789UL] = new("original_puppet", "傀儡雕像", "呼朋唤友，提线木偶", false),
        [16161024687220126744UL] = new("original_camera", "摄像雕像", "第二人称，容易头昏", false),
        [6096484020692488796UL] = new("original_prankster", "传送雕像", "跳刀闪现，瞬间不见", false),
        [13539466722732671913UL] = new("original_heavy", "重型雕像", "不痒不痛，单纯很重", false),
        [3611313213204598715UL] = new("original_hallucination", "幻觉雕像", "亦假亦真，小心别疯", false),
        [14189525892065481450UL] = new("original_meduza", "凝视雕像", "瞅你咋地，看我干啥", false),
        [10036870583278101750UL] = new("original_linked", "绳索雕像", "两两相绑，有福同享", false),
        [887832889495486297UL] = new("original_final", "", "拿了快跑，回家才好", false),
        [17513984537194232416UL] = new("original_missing_piece", "", "还没玩到，莫名其妙", false)
    };

    internal static bool TryGet(ulong configId, out IdolHoverText text)
        => Texts.TryGetValue(configId, out text);
}
