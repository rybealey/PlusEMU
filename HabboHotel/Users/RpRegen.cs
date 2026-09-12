namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp consumables: a bar filling itself back up after a snack or a medkit.
///
/// The rate is the whole bar over a minute, so a bar emptied to nothing takes
/// the full sixty seconds and a bar half gone takes thirty. One rate, read off
/// the maximum, rather than "always sixty seconds however little is missing" -
/// which would make a snack taken at 99 energy slower than one taken at 1.
///
/// Fractional, because a hundred points over sixty seconds is 1.67 a second and
/// the room tick runs faster than that. Carry holds the part of a point that
/// has been earned but not yet handed over; without it every tick would round
/// its own fraction away and the bar would crawl or never arrive at all.
///
/// Transient, like aggression: nothing here is persisted, so a regen ends when
/// the player logs out rather than resuming days later.
/// </summary>
public sealed class RpRegen
{
    /// <summary>How long an empty bar takes to come back, in seconds.</summary>
    public const int SecondsToFull = 60;

    private double _perSecond;
    private double _carry;
    private long _lastTick;

    /// <summary>Is a consumable still working?</summary>
    public bool Running => _perSecond > 0;

    /// <summary>
    /// Begin filling a bar whose maximum is <paramref name="max"/>.
    ///
    /// Restarting replaces whatever was running rather than stacking with it:
    /// two snacks at once should not fill twice as fast, and the use handlers
    /// refuse a second one anyway.
    /// </summary>
    public void Start(int max)
    {
        _perSecond = (max > 0) ? max / (double)SecondsToFull : 0;
        _carry = 0;
        _lastTick = 0;
    }

    /// <summary>
    /// Stop, whether because the bar is full or because something interrupted
    /// it. Deliberately keeps no memory: an interrupted medkit is spent, and
    /// the remaining health is not owed to anybody.
    /// </summary>
    public void Stop()
    {
        _perSecond = 0;
        _carry = 0;
        _lastTick = 0;
    }

    /// <summary>
    /// Whole points earned since the last call, zero if nothing is running.
    ///
    /// The first call only starts the clock. That matters on a room change:
    /// the regen rides along on the Habbo, and anchoring here rather than at
    /// Start means the time is measured from a tick that actually happened.
    /// </summary>
    public int Advance(long nowTick)
    {
        if (!Running)
            return 0;
        if (_lastTick == 0)
        {
            _lastTick = nowTick;
            return 0;
        }
        var elapsed = (nowTick - _lastTick) / 1000.0;
        if (elapsed <= 0)
            return 0;
        _lastTick = nowTick;
        _carry += elapsed * _perSecond;
        var whole = (int)_carry;
        if (whole <= 0)
            return 0;
        _carry -= whole;
        return whole;
    }
}
