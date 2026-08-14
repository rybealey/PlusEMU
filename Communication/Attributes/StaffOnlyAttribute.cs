namespace Plus.Communication.Attributes;

/// <summary>
/// Packets carrying this attribute are only executed for staff sessions (Habbo.IsStaff).
/// Anyone else sending one has bypassed the client UI (packet injection), so the packet
/// is dropped at the PacketManager and the attempt is logged.
/// </summary>
public class StaffOnlyAttribute : Attribute
{
}
