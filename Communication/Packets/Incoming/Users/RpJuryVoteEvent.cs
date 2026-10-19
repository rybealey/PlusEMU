using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a juror's Guilty (1) or Not Guilty (2) on the vote card. Everything
/// is checked in JuryTrials.Vote.
/// </summary>
internal class RpJuryVoteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var vote = packet.ReadInt();
        if (session.GetHabbo() != null)
            JuryTrials.Vote(session, vote);
        return Task.CompletedTask;
    }
}
