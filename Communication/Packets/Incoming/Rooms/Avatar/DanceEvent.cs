using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Avatar;

internal class DanceEvent : RoomPacketEvent
{
    private readonly IQuestManager _questManager;

    public DanceEvent(IQuestManager questManager)
    {
        _questManager = questManager;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return Task.CompletedTask;
        if (KnockedOut.Refuse(session))
            return Task.CompletedTask;
        user.UnIdle();
        var danceId = packet.ReadInt();
        if (danceId < 0 || danceId > 4)
            danceId = 0;
        // pixelrp: a dance keeps what is in the hand (Twist, 2026-10-15) - the
        // drink, the weapon, the open phone. The client used to hide a carry
        // while dancing (Dance prevented "cri" in HabboAvatarActions); the
        // gamedata-override puts it back, so there is nothing to drop.
        if (session.GetHabbo().Effects.CurrentEffect > 0)
            room.SendPacket(new AvatarEffectComposer(user.VirtualId, 0));
        user.DanceId = danceId;
        room.SendPacket(new DanceComposer(user, danceId));
        // pixelrp: a dance hides the enable, and stopping puts it back - for
        // everyone, not only the Ghost. The strip above only tells the room -
        // CurrentEffect is untouched - so the tick would never send it again,
        // and a passive, staff-duty or cuffed player who danced lost it from
        // every screen until they changed rooms.
        if (danceId == 0 && session.GetHabbo().Effects.CurrentEffect > 0)
            room.SendPacket(new AvatarEffectComposer(user.VirtualId, session.GetHabbo().Effects.CurrentEffect));
        _questManager.ProgressUserQuest(session, QuestType.SocialDance);
        if (room.GetRoomUserManager().GetRoomUsers().Count > 19)
            _questManager.ProgressUserQuest(session, QuestType.MassDance);
        return Task.CompletedTask;
    }
}