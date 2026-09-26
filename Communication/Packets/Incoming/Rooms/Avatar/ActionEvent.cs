using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Avatar;

public class ActionEvent : RoomPacketEvent
{
    private readonly IQuestManager _questManager;

    public ActionEvent(IQuestManager questManager)
    {
        _questManager = questManager;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var action = packet.ReadInt();
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return Task.CompletedTask;
        // pixelrp: out cold, no waving. Going idle (5) can be sent without the
        // player asking, so it is dropped quietly rather than answered.
        if (action == 5 ? KnockedOut.Is(session) : KnockedOut.Refuse(session))
            return Task.CompletedTask;
        if (user.DanceId > 0)
            user.DanceId = 0;
        if (session.GetHabbo().Effects.CurrentEffect > 0)
            room.SendPacket(new AvatarEffectComposer(user.VirtualId, 0));
        user.UnIdle();
        room.SendPacket(new ActionComposer(user.VirtualId, action));
        if (action == 5) // idle
        {
            user.IsAsleep = true;
            room.SendPacket(new SleepComposer(user, true));
        }
        _questManager.ProgressUserQuest(session, QuestType.SocialWave);
        return Task.CompletedTask;
    }
}