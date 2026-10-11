using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.AI.Types;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Bots;

/// <summary>
/// pixelrp: "Buy membership" from a Gym bot's menu - the bot's virtual id.
///
/// RpTellerActionEvent's twin: the menu only offers it on a bot the Gym bot
/// push named, but that is an affordance, so this confirms the avatar is a Gym
/// bot in the player's own room, and the bot re-checks the range. A bad id is
/// silence.
/// </summary>
internal class RpGymBotActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;

        var virtualId = packet.ReadInt();
        var bot = room.GetRoomUserManager()?.GetRoomUserByVirtualId(virtualId);
        if (bot == null || !bot.IsBot || bot.IsPet || bot.BotAi is not GymBot gym)
            return Task.CompletedTask;

        gym.Serve(session);
        return Task.CompletedTask;
    }
}
