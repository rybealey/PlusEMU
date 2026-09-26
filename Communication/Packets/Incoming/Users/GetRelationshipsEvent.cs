using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Relationships;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// The infostand's relationship rows, answered from partnerships rather than the
/// friends list: a partner is the single Love row, and nothing else is listed.
/// See PartnershipUtility for why they are no longer read off
/// messenger_friendships.
/// </summary>
internal class GetRelationshipsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        session.Send(new GetRelationshipsComposer(userId, PartnershipUtility.RelationshipsFor(userId)));
        return Task.CompletedTask;
    }
}
