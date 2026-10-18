using CalypsoExpansionSet.UI;

namespace CalypsoExpansionSet.Lobby;

internal readonly record struct LobbyBoxStrings(string Title, string Introduction, string Read, string Close,
    string Scroll, string Credits, string Development, string Modeling, string Artwork);

internal static class LobbyBoxText
{
    internal static UiLanguage Language => IdolHoverInfo.CurrentLanguage;
    // The editor, lighting presets and placement reports remain bilingual debug UI.
    internal static string Pick(string zh, string en) => UiLanguages.IsChinese(Language) ? zh : en;
    internal static LobbyBoxStrings Current => For(Language);
    internal static string Title => Current.Title;
    internal const string ChineseIntroduction =
        "欢迎游玩《卡里普索的拓展包》！\n\n"
        + "本拓展包为您带来：\n\n"
        + "游戏性提升：\n・添加很多有趣又富有挑战性的雕像\n"
        + "・骰子会带来新的诅咒：你会变成其他人。大师棋盘也有几率出现该规则。\n\n"
        + "生活质量提升：\n・移除棋盘上棋子的碰撞，方便踢骰子\n"
        + "・为所有雕像提供箴言和名称提示，可分别配置（箴言默认打开，名称默认关闭）\n"
        + "・在大厅时，头像名字变为青色\n"
        + "・进入商店时无限体力\n\n"
        + "祝您游戏愉快！:3";
    internal const string EnglishIntroduction =
        "Welcome to Calypso's Expansion Set!\n\n"
        + "Here's what this expansion brings you:\n\n"
        + "Gameplay improvements:\n· Lots of fun and challenging new idols\n"
        + "· Dice bring a new curse: you become someone else. Master boards also have a chance to include this rule.\n\n"
        + "Quality of life improvements:\n· Removes board-pawn collisions to make kicking dice easier\n"
        + "· Adds advice and individual names for all idols, configurable separately (advice on, names off by default)\n"
        + "· Portrait names turn cyan while players are in the lobby\n"
        + "· Unlimited stamina while in the shop\n\n"
        + "Have fun! :3";
    private const string TraditionalIntroduction =
        "歡迎遊玩《卡里普索的拓展包》！\n\n"
        + "本拓展包為您帶來：\n\n"
        + "遊戲性提升：\n・新增許多有趣又富有挑戰性的雕像\n"
        + "・骰子會帶來新的詛咒：你會變成其他人。大師棋盤也有機率出現該規則。\n\n"
        + "生活品質提升：\n・移除棋盤上棋子的碰撞，方便踢骰子\n"
        + "・為所有雕像提供箴言和名稱提示，可分別設定（箴言預設開啟，名稱預設關閉）\n"
        + "・在大廳時，頭像名字變為青色\n"
        + "・進入商店時無限耐力\n\n"
        + "祝您遊戲愉快！:3";
    private const string JapaneseIntroduction =
        "『カリプソの拡張セット』へようこそ！\n\n"
        + "この拡張セットで楽しめること：\n\n"
        + "新しい遊び：\n・楽しくて手ごわい像がたくさん登場\n"
        + "・サイコロがもたらす新たな呪い：あなたは別の誰かになります。マスターのボードにもこのルールが登場することがあります。\n\n"
        + "遊びやすさの改善：\n・ボード上のコマの当たり判定をなくし、サイコロを蹴りやすくします\n"
        + "・すべての像に短い詩のヒントと個別の名前を追加。それぞれ設定可能です（初期設定：ヒントはオン、名前はオフ）\n"
        + "・ロビーにいるプレイヤーは、ポートレートの名前が水色になります\n"
        + "・ショップ内ではスタミナが無制限になります\n\n"
        + "どうぞお楽しみください！:3";
    private const string RussianIntroduction =
        "Добро пожаловать в набор дополнений Калипсо!\n\n"
        + "Что вас ждёт:\n\n"
        + "Новые возможности:\n· Множество забавных идолов, с которыми придётся повозиться\n"
        + "· Кубики приносят новое проклятие: вы становитесь кем-то другим. На досках мастера также может появиться это правило.\n\n"
        + "Удобство игры:\n· Фишки на игровом поле больше не мешают пинать кубики\n"
        + "· Подсказки в стихах и отдельные имена для всех идолов. Настраиваются независимо (подсказки включены, имена выключены по умолчанию)\n"
        + "· Имена на портретах становятся бирюзовыми, пока игроки находятся в лобби\n"
        + "· В магазине запас выносливости не ограничен\n\n"
        + "Приятной игры! :3";
    private const string PortugueseIntroduction =
        "Boas-vindas ao pacote de expansão de Calypso!\n\n"
        + "Veja o que esta expansão traz:\n\n"
        + "Novidades na jogabilidade:\n· Muitos ídolos divertidos e desafiadores\n"
        + "· Os dados trazem uma nova maldição: você se torna outra pessoa. Tabuleiros de mestre também têm chance de incluir essa regra.\n\n"
        + "Melhorias de qualidade de vida:\n· Remove a colisão das peças do tabuleiro para facilitar os chutes nos dados\n"
        + "· Adiciona dicas em versos e nomes individuais para todos os ídolos, configuráveis separadamente (dicas ativadas e nomes desativados por padrão)\n"
        + "· Os nomes nos retratos ficam em ciano enquanto os jogadores estão no lobby\n"
        + "· Vigor ilimitado dentro da loja\n\n"
        + "Divirta-se! :3";
    private const string SpanishIntroduction =
        "¡Te damos la bienvenida a la expansión de Calypso!\n\n"
        + "Esto es lo que trae esta expansión:\n\n"
        + "Novedades de juego:\n· Muchos ídolos divertidos y desafiantes\n"
        + "· Los dados traen una nueva maldición: te conviertes en otra persona. Los tableros de maestro también pueden incluir esta regla.\n\n"
        + "Mejoras de comodidad:\n· Elimina las colisiones de las fichas del tablero para facilitar las patadas a los dados\n"
        + "· Añade pistas en verso y nombres individuales para todos los ídolos, configurables por separado (pistas activadas y nombres desactivados por defecto)\n"
        + "· Los nombres de los retratos se vuelven cian mientras los jugadores están en la sala\n"
        + "· Resistencia ilimitada dentro de la tienda\n\n"
        + "¡Que te diviertas! :3";

    private static readonly Dictionary<UiLanguage, LobbyBoxStrings> Texts = new()
    {
        [UiLanguage.English] = new("Calypso's Expansion Set", EnglishIntroduction, "Read introduction", "Close",
            "Scroll: mouse wheel / movement controls", "Credits", "Development", "Modeling", "Character artwork"),
        [UiLanguage.SimplifiedChinese] = new("卡里普索的拓展包", ChineseIntroduction, "查看介绍", "关闭",
            "滚轮／移动键／摇杆：上下滚动", "制作人员", "开发", "模型制作", "卡通形象绘制"),
        [UiLanguage.TraditionalChinese] = new("卡里普索的拓展包", TraditionalIntroduction, "查看介紹", "關閉",
            "滾輪／移動鍵／搖桿：上下捲動", "製作人員", "開發", "模型製作", "卡通形象繪製"),
        [UiLanguage.Japanese] = new("カリプソの拡張セット", JapaneseIntroduction, "紹介を読む", "閉じる",
            "スクロール：マウスホイール／移動キー／スティック", "制作スタッフ", "開発", "モデル制作", "キャラクターイラスト"),
        [UiLanguage.Russian] = new("Набор дополнений Калипсо", RussianIntroduction, "Прочитать описание", "Закрыть",
            "Прокрутка: колесо мыши / движение / стик", "Авторы", "Разработка", "Модели", "Рисунки персонажей"),
        [UiLanguage.Portuguese] = new("Pacote de expansão de Calypso", PortugueseIntroduction, "Ler apresentação", "Fechar",
            "Rolar: roda do mouse / controles de movimento", "Créditos", "Desenvolvimento", "Modelagem", "Arte dos personagens"),
        [UiLanguage.Spanish] = new("Expansión de Calypso", SpanishIntroduction, "Leer presentación", "Cerrar",
            "Desplazar: rueda del ratón / controles de movimiento", "Créditos", "Desarrollo", "Modelado", "Arte de personajes")
    };

    internal static LobbyBoxStrings For(UiLanguage language) =>
        Texts.TryGetValue(language, out var text) ? text : Texts[UiLanguage.English];
    internal static string Introduction => Current.Introduction;

    // Nicknames are identities, not prose to translate. Chinese variants retain
    // the confirmed Chinese names; other languages use the existing Latin names.
    internal static string CreditName(int index, UiLanguage language) => index switch
    {
        0 => UiLanguages.IsChinese(language) ? "尤里的猫卡里普索" : "YurisCat",
        1 => UiLanguages.IsChinese(language) ? "马赛克了" : "Masaicker",
        2 => UiLanguages.IsChinese(language) ? "番茄鱼F" : "Tomato_Fish",
        _ => "Charrant"
    };
}
