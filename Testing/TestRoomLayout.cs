namespace CalypsoExpansionSet.Testing;

internal static class TestRoomLayout
{
    internal const int Columns = 10;
    internal const float Spacing = 2.2f;
    internal const float RowSpacing = 3.2f;
    internal const float Width = 28f;
    internal const float EntranceLength = 6f;
    internal const float ModStart = 14f;

    internal static (float X, float Z) ModSlot(int index, int count) => Slot(index, count, ModStart);
    internal static (float X, float Z) OriginalSlot(int index, int count, int modCount)
        => Slot(index, count, OriginalStart(modCount));

    private static (float X, float Z) Slot(int index, int count, float start)
    {
        ValidateCount(count, false);
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        var row = index / Columns;
        var rowCount = Math.Min(Columns, count - row * Columns);
        return ((index % Columns - (rowCount - 1) * 0.5f) * Spacing, start + row * RowSpacing);
    }

    internal static float OriginalStart(int modCount)
    {
        ValidateCount(modCount, true);
        // Keep the original collection at the back, even as new MOD rows are added.
        return Math.Max(28.8f, ModStart + Math.Max(0, (modCount - 1) / Columns) * RowSpacing + 6.4f);
    }

    internal static float End(int originalCount, int modCount)
        => Math.Max(36f, OriginalSlot(originalCount - 1, originalCount, modCount).Z + 4f);

    private static void ValidateCount(int count, bool allowEmpty)
    {
        if (count < (allowEmpty ? 0 : 1) || count > 128)
            throw new ArgumentOutOfRangeException(nameof(count), "A test gallery supports up to 128 types per section.");
    }
}
