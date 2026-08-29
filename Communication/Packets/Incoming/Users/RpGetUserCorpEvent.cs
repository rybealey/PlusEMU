using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a profile opened - send that user's employment state so the
/// profile's corporation row is correct even for users outside the room.
/// </summary>
internal class RpGetUserCorpEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (session.GetHabbo() == null || userId <= 0)
            return Task.CompletedTask;
        session.Send(CorporationUtility.ComposeFor(userId, CorporationUtility.GetEmployment(userId)));
        return Task.CompletedTask;
    }
}
