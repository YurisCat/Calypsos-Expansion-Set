namespace CalypsoExpansionSet.UI;

internal sealed record ExpansionSettingsStrings(string EntryTitle, string PageTitle, string Names, string Advice,
    string Natural, string Clock, string Apply, string Back, string SaveFailed,
    string PingMouse, string PingGamepad, string Disabled, string RebindMouseHint, string RebindGamepadHint);

internal static class ExpansionSettingsText
{
    internal static ExpansionSettingsStrings For(UiLanguage language) => language switch
    {
        UiLanguage.SimplifiedChinese => new("拓展包设置", "卡里普索的拓展包", "具体名称（仅自己）", "顶部箴言（仅自己）",
            "雕像自然生成（房主）", "挂钟反复使用（房主）", "应用并返回", "返回",
            "设置保存失败，请检查文件写入权限后重试",
            "队友标记（键鼠）", "队友标记（手柄）", "未绑定",
            "请按键盘或鼠标的新按键；Esc 取消",
            "请按手柄的新按键；B / ○ / Esc 取消"),
        UiLanguage.TraditionalChinese => new("拓展包設定", "卡里普索的拓展包", "個別名稱（僅自己）", "頂部箴言（僅自己）",
            "雕像自然生成（房主）", "掛鐘反覆使用（房主）", "套用並返回", "返回",
            "設定儲存失敗，請檢查檔案寫入權限後重試",
            "隊友標記（鍵鼠）", "隊友標記（手把）", "未綁定",
            "請按鍵盤或滑鼠的新按鍵；Esc 取消",
            "請按手把的新按鍵；B / ○ / Esc 取消"),
        UiLanguage.Japanese => new("拡張セットの設定", "カリプソの拡張セット", "像の個別名（自分のみ）", "像のヒント（自分のみ）",
            "像の自然出現（ホスト）", "古時計の再使用（ホスト）", "適用して戻る", "戻る",
            "設定を保存できません。ファイルの書き込み権限を確認してください",
            "味方へのピン（キー・マウス）", "味方へのピン（パッド）", "未設定",
            "新しいキーかマウスボタンを押してください。Esc：キャンセル",
            "新しいパッドのボタンを押してください。B / ○ / Esc：キャンセル"),
        UiLanguage.Russian => new("Настройки дополнения", "Набор дополнений Калипсо", "Имена идолов (для себя)", "Подсказки (для себя)",
            "Появление идолов (хост)", "Многоразовые часы (хост)", "Применить и назад", "Назад",
            "Не удалось сохранить настройки. Проверьте права записи и повторите попытку",
            "Метка для команды (мышь/клавиши)", "Метка для команды (геймпад)", "Не назначено",
            "Нажмите новую клавишу или кнопку мыши. Esc: отмена",
            "Нажмите новую кнопку геймпада. B / ○ / Esc: отмена"),
        UiLanguage.Portuguese => new("Configurações da expansão", "Pacote de expansão de Calypso", "Nomes dos ídolos (local)", "Dicas dos ídolos (local)",
            "Aparição de ídolos (anfitrião)", "Relógio reutilizável (anfitrião)", "Aplicar e voltar", "Voltar",
            "Falha ao salvar. Verifique a permissão de escrita e tente novamente",
            "Marcar para a equipe (mouse/teclado)", "Marcar para a equipe (controle)", "Sem botão",
            "Pressione a nova tecla ou botão do mouse. Esc: cancelar",
            "Pressione o novo botão do controle. B / ○ / Esc: cancelar"),
        UiLanguage.Spanish => new("Ajustes de la expansión", "Expansión de Calypso", "Nombres de ídolos (local)", "Pistas de ídolos (local)",
            "Aparición de ídolos (anfitrión)", "Reloj reutilizable (anfitrión)", "Aplicar y volver", "Volver",
            "No se pudieron guardar los ajustes. Comprueba los permisos de escritura e inténtalo de nuevo",
            "Marca de equipo (ratón/teclado)", "Marca de equipo (mando)", "Sin asignar",
            "Pulsa la nueva tecla o botón del ratón. Esc: cancelar",
            "Pulsa el nuevo botón del mando. B / ○ / Esc: cancelar"),
        _ => new("Expansion settings", "Calypso's Expansion Set", "Individual idol names (local)", "Idol advice (local)",
            "Natural idol spawning (host)", "Reusable Old Clock (host)", "Apply and back", "Back",
            "Could not save settings. Check file write permissions and try again",
            "Team ping (mouse/keyboard)", "Team ping (gamepad)", "Unbound",
            "Press a new key or mouse button. Esc: cancel",
            "Press a new gamepad button. B / ○ / Esc: cancel")
    };
}
