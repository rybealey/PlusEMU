using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client reports who is selected in the player's HUD - a user
/// id, 0 when nobody is - on every change. A target command typed without a
/// name is aimed at them (CommandManager); nothing else reads it.
/// </summary>
internal class RpSetHudTargetEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        session.GetHabbo().RpHudTargetId = userId > 0 && userId != session.GetHabbo().Id ? userId : 0;
        return Task.CompletedTask;
    }
}
