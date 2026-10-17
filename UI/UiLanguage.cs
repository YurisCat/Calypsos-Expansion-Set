namespace CalypsoExpansionSet.UI;

internal enum UiLanguage
{
    English,
    SimplifiedChinese,
    TraditionalChinese,
    Japanese,
    Russian,
    Portuguese,
    Spanish
}

// Pure language selection. Native I2 strings are sampled by IdolHoverInfo,
// never read from interaction getters or translated into network state.
internal static class UiLanguages
{
    internal static bool IsChinese(UiLanguage language) =>
        language is UiLanguage.SimplifiedChinese or UiLanguage.TraditionalChinese;

    internal static UiLanguage Resolve(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var value = code.Trim().Replace('_', '-');
            if (Code(value, "zh-Hant") || Code(value, "zh-TW") || Code(value, "zh-HK")
                || Code(value, "zh-MO") || Code(value, "zh-CHT"))
                return UiLanguage.TraditionalChinese;
            if (Code(value, "zh")) return UiLanguage.SimplifiedChinese;
            if (Code(value, "ja")) return UiLanguage.Japanese;
            if (Code(value, "ru")) return UiLanguage.Russian;
            if (Code(value, "pt")) return UiLanguage.Portuguese;
            if (Code(value, "es")) return UiLanguage.Spanish;
            return UiLanguage.English;
        }

        // I2's name is a fallback only when its language code is unavailable.
        var language = name?.Trim().Replace('_', ' ');
        if (language == null) return UiLanguage.English;
        if (language.Equals("Traditional chinese", StringComparison.OrdinalIgnoreCase)
            || language.Equals("繁體中文", StringComparison.Ordinal)
            || language.Equals("繁体中文", StringComparison.Ordinal))
            return UiLanguage.TraditionalChinese;
        if (Named(language, "Chinese"))
            return language.Contains("Traditional", StringComparison.OrdinalIgnoreCase)
                ? UiLanguage.TraditionalChinese : UiLanguage.SimplifiedChinese;
        if (language.Equals("Simplified chinese", StringComparison.OrdinalIgnoreCase)
            || language.Equals("简体中文", StringComparison.Ordinal)
            || language.Equals("簡體中文", StringComparison.Ordinal))
            return UiLanguage.SimplifiedChinese;
        if (Named(language, "Japanese") || language == "日本語") return UiLanguage.Japanese;
        if (Named(language, "Russian") || language.Equals("Русский", StringComparison.OrdinalIgnoreCase))
            return UiLanguage.Russian;
        if (Named(language, "Portuguese") || Named(language, "Português")) return UiLanguage.Portuguese;
        if (Named(language, "Spanish") || Named(language, "Español")) return UiLanguage.Spanish;
        return UiLanguage.English;
    }

    private static bool Code(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && (value.Length == prefix.Length || value[prefix.Length] == '-');

    private static bool Named(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && (value.Length == prefix.Length || value[prefix.Length] is ' ' or '/');
}
