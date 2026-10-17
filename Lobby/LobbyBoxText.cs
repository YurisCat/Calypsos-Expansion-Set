namespace CalypsoExpansionSet.Lobby;

internal static class LobbyBoxText
{
    internal static bool Chinese => UI.IdolTextLocalization.IsChinese(
        I2.Loc.LocalizationManager.CurrentLanguageCode, I2.Loc.LocalizationManager.CurrentLanguage);
    internal static string Pick(string zh, string en) => Chinese ? zh : en;
    internal static string Title => Pick("卡里普索的拓展包", "Calypso's Expansion Set");
    internal const string ChineseIntroduction =
        "欢迎游玩《卡里普索的拓展包》！\n\n"
        + "本拓展包为您带来：\n\n"
        + "游戏性提升：\n・添加很多有趣又富有挑战性的雕像\n\n"
        + "生活质量提升：\n・移除棋盘上棋子的碰撞，方便踢骰子\n"
        + "・为所有雕像提供箴言和名称提示，可分别配置（箴言默认打开，名称默认关闭）\n"
        + "・在大厅时，头像名字变为青色\n"
        + "・进入商店时无限体力\n\n"
        + "祝您游戏愉快！:3";
    internal const string EnglishIntroduction =
        "Welcome to Calypso's Expansion Set!\n\n"
        + "Here's what this expansion brings you:\n\n"
        + "Gameplay improvements:\n· Lots of fun and challenging new idols\n\n"
        + "Quality of life improvements:\n· Removes board-pawn collisions to make kicking dice easier\n"
        + "· Adds advice and individual names for all idols, configurable separately (advice on, names off by default)\n"
        + "· Portrait names turn cyan while players are in the lobby\n"
        + "· Unlimited stamina while in the shop\n\n"
        + "Have fun! :3";
    internal static string Introduction => Pick(ChineseIntroduction, EnglishIntroduction);
}
