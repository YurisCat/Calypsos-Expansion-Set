namespace CalypsoExpansionSet.Testing;

internal static class TestRoomMessageFilter
{
    internal const string JoinWarningTerm = "UI/lobby/waitPlayerBeforeStart";

    internal static bool ShouldSuppress(bool testActive, string? message, string? translatedWarning) =>
        testActive && !string.IsNullOrEmpty(message)
        && (string.Equals(message, JoinWarningTerm, StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(translatedWarning)
                && string.Equals(message, translatedWarning, StringComparison.Ordinal)));
}
