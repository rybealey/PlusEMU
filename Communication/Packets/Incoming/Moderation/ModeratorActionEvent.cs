using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal class ModeratorActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().Permissions.HasRight("mod_caution"))
            return Task.CompletedTask;
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var currentRoom = session.GetHabbo().CurrentRoom;
        if (currentRoom == null)
            return Task.CompletedTask;
        packet.ReadInt(); // alert mode (caution/message) — same toast either way
        var alertMessage = packet.ReadString();
        // pixelrp: room alerts render as the blue Information toast for everyone
        // in the room; the badge replaces the old "from Moderator" prefixes.
        currentRoom.SendPacket(new RoomNotificationComposer("room.alert",
            new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", alertMessage } }));
        return Task.CompletedTask;
    }
}