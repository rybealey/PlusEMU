using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

/// <summary>pixelrp: Tunes app opened - tell the phone whether this player gets the staff controls.</summary>
internal class RpGetTunesAccessEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        session.Send(new RpTunesAccessComposer(JukeboxStation.IsStationStaff(session)));
        return Task.CompletedTask;
    }
}
