using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Request a Trial button on a prisoner's jury card. No payload -
/// it is always their own sentence. Everything is checked in
/// JailState.RequestTrial.
/// </summary>
internal class RpRequestTrialEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() != null)
            JailState.RequestTrial(session, typed: false);
        return Task.CompletedTask;
    }
}
