using Plus.Communication.Packets.Incoming.Rooms;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Users;

internal class RespectUserEvent : RoomPacketEvent
{
    private readonly IAchievementManager _achievementManager;
    private readonly IQuestManager _questManager;

    public RespectUserEvent(IAchievementManager achievementManager, IQuestManager questManager)
    {
        _achievementManager = achievementManager;
        _questManager = questManager;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // Respect system disabled — ignore the packet entirely.
        return Task.CompletedTask;
    }
}