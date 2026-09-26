using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: relationships are no longer self-declared, so this does nothing.
///
/// The stock client let anyone pin a heart, a smile or a skull on a friend from
/// the avatar menu. A relationship is earned now - a partnership made with
/// :propose and ended with :divorce (see PartnershipUtility) - so the menu no
/// longer offers the choice, and this swallows the packet for any client that
/// still sends it. Registered rather than deleted so an old client's packet is
/// quietly ignored instead of logged as an unknown header.
/// </summary>
internal class SetRelationshipEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => Task.CompletedTask;
}
