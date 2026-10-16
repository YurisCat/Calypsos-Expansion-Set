namespace CalypsoExpansionSet.Lobby;

internal static class LobbyBoxText
{
    internal static bool Chinese => UI.IdolTextLocalization.IsChinese(
        I2.Loc.LocalizationManager.CurrentLanguageCode, I2.Loc.LocalizationManager.CurrentLanguage);
    internal static string Pick(string zh, string en) => Chinese ? zh : en;
    internal static string Title => Pick("卡里普索的拓展包", "Calypso's Expansion Set");
    internal const string ChineseIntroduction =
        "欢迎游玩《卡里普索的拓展包》！本拓展包为您带来！\n\n"
        + "游戏性提升：\n·添加很多有趣又富有挑战性的雕像\n\n"
        + "生活质量提升：\n·移除玩家棋子的碰撞，方便玩家在棋盘上踢骰子\n"
        + "·为所有的（包括原版）雕像提供箴言\n"
        + "·直接显示所有雕像的名字（需在配置文件中开启）\n\n"
        + "祝您游戏愉快！:3";
    internal const string EnglishIntroduction =
        "Welcome to Calypso's Expansion Set! Here's what this expansion brings you!\n\n"
        + "Gameplay improvements:\n· Lots of fun and challenging new idols\n\n"
        + "Quality of life improvements:\n· Removes player-pawn collisions, making it easier to kick dice on the board\n"
        + "· Adds words of advice for every idol, including vanilla idols\n"
        + "· Shows individual names for all idols (enable this in the configuration file)\n\n"
        + "Have fun! :3";
    internal static string Introduction => Chinese ? ChineseIntroduction : EnglishIntroduction;
}
