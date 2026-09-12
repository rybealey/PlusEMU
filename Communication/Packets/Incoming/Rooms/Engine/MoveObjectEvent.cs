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
        // pixelrp: where the piece was, for :undo. Taken before the move and only
        // kept if the move actually happens - a refused move changes nothing, and
        // recording it would throw away a snapshot the builder can still use.
        var undoState = FurniUndoState.Capture(item);

        // pixelrp: same build height as a fresh placement - dragging a piece while
        // :bh is on re-levels it rather than dropping it back onto the stack.
        //
        // With :bh off, a height the builder set on THIS item in the Tools panel
        // is re-applied instead, so dragging a piece across the room does not
        // quietly drop it back onto the stack. :bh wins when both are set: it is
        // the deliberate "everything I touch sits here" mode.
        var buildHeight = room.GetRoomUserManager().BuildHeightFor(session.GetHabbo().Id);
        if (!room.GetRoomItemHandler().SetFloorItem(session, item, x, y, rotation, false, false, true,
                height: (buildHeight >= 0) ? buildHeight : item.CustomHeight))
        {
            room.SendPacket(new ObjectUpdateComposer(item));
            return Task.CompletedTask;
        }
        if (item.GetZ >= 0.1)
            _questManager.ProgressUserQuest(session, QuestType.FurniStack);

        // The move stuck, so this is now the thing :undo puts back.
        var mover = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (mover != null)
            mover.LastFurniUndo = undoState;

        return Task.CompletedTask;
    }
}