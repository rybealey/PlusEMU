namespace Plus.HabboHotel.Moderation;

/// <summary>
/// Mirrors the `status` enum on `moderation_tickets`. The member names must keep
/// lower-casing to exactly the database's values — that is the entire mapping.
/// </summary>
public enum ModerationTicketStatus
{
    Open,
    Picked,
    Resolved,
    Abusive,
    Invalid,
    Deleted
}
