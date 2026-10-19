namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal enum PingPressAction { None, Mark, Clear }

internal sealed class PingPress
{
    internal const double HoldSeconds = .5;
    private bool _armed, _holding;
    private double _started;

    internal void Reset() => _armed = _holding = false;

    internal PingPressAction Update(bool held, bool pressedThisFrame, double now)
    {
        if (_holding)
        {
            if (now - _started >= HoldSeconds)
            {
                _holding = _armed = false;
                return PingPressAction.Clear;
            }
            if (!held)
            {
                _holding = false; _armed = true;
                return PingPressAction.Mark;
            }
            return PingPressAction.None;
        }
        if (!held) _armed = true;
        if (!_armed || !pressedThisFrame) return PingPressAction.None;
        _armed = false;
        // Both edges can occur between two rendered frames.
        if (!held) return PingPressAction.Mark;
        _holding = true; _started = now;
        return PingPressAction.None;
    }
}
