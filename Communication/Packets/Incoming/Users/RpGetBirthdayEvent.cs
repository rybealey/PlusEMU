using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Birthdays;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the phone's Account screen asks for the player's birthday.</summary>
internal class RpGetBirthdayEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() != null)
            BirthdayUtility.SendBirthday(session);
        return Task.CompletedTask;
    }
}
