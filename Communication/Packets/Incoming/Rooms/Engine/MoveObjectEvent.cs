using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class MoveObjectEvent : RoomPacketEvent
{
    private readonly IRoomManager _roomManager;
    private readonly IQuestManager _questManager;

    public MoveObjectEvent(IRoomManager roomManager, IQuestManager questManager)
    {
        _roomManager = roomManager;
        _questManager = questManager;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadUInt();
        if (itemId == 0)
            return Task.CompletedTask;
        Item item;
        if (room.Group != null)
        {
            if (!room.CheckRights(session, false, true))
            {
                item = room.GetRoomItemHandler().GetItem(itemId);
                if (item == null)
                    return Task.CompletedTask;
                session.Send(new ObjectUpdateComposer(item));
                return Task.CompletedTask;
            }
        }
        else
        {
            if (!room.CheckRights(session)) return Task.CompletedTask;
        }
        item = room.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return Task.CompletedTask;
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var rotation = packet.ReadInt();
        if (x != item.GetX || y != item.GetY)
            _questManager.ProgressUserQuest(session, QuestType.FurniMove);
        if (rotation != item.Rotation)
            _questManager.ProgressUserQuest(session, QuestType.FurniRotate);
        // pixelrp: same build height as a fresh placement - dragging a piece while
        // :bh is on re-levels it rather than dropping it back onto the stack.
        if (!room.GetRoomItemHandler().SetFloorItem(session, item, x, y, rotation, false, false, true,
                height: room.GetRoomUserManager().BuildHeightFor(session.GetHabbo().Id)))
        {
            room.SendPacket(new ObjectUpdateComposer(item));
            return Task.CompletedTask;
        }
        if (item.GetZ >= 0.1)
            _questManager.ProgressUserQuest(session, QuestType.FurniStack);
        return Task.CompletedTask;
    }
}