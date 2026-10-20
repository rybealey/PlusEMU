namespace Plus.Communication.Attributes;

/// <summary>
/// Packets carrying this attribute are only executed for Builder or above
/// (Habbo.IsBuilder) - the Furni Shop and the navigator, which Builders need and
/// which sit below the staff line. Anyone else sending one has bypassed the client
/// UI (packet injection), so the packet is dropped at the PacketManager and the
/// attempt is logged.
/// </summary>
public class BuilderOnlyAttribute : Attribute
{
}
