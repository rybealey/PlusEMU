using Plus.Core.Language;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal class ModerationKickEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;
    private readonly ILanguageManager _languageManager;

    public ModerationKickEvent(IGameClientManager clientManager, ILanguageManager languageManager)
    {
        _clientManager = clientManager;
        _languageManager = languageManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().Permissions.HasRight("mod_kick"))
            return Task.CompletedTask;
        var userId = packet.ReadInt();
        var message = packet.ReadString();
        var client = _clientManager.GetClientByUserId(userId);
        if (client == null || client.GetHabbo() == null || client.GetHabbo().CurrentRoom == null || client.GetHabbo().Id == session.GetHabbo().Id)
            return Task.CompletedTask;
        if (client.GetHabbo().Rank >= session.GetHabbo().Rank)
        {
            session.SendNotification(_languageManager.TryGetValue("moderation.kick.disallowed"));
            return Task.CompletedTask;
        }
        // pixelrp: the kick message used to be read and discarded — deliver it.
        if (!string.IsNullOrWhiteSpace(message))
            client.SendModerationAlert(message);
        session.GetHabbo().CurrentRoom?.GetRoomUserManager().RemoveUserFromRoom(client, true);
        return Task.CompletedTask;
    }
}