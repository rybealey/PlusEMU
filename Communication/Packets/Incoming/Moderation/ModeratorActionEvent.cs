using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal class ModeratorActionEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public ModeratorActionEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().Permissions.HasRight("mod_caution"))
            return Task.CompletedTask;
        packet.ReadInt(); // alert mode (caution/message) - same toast either way
        var alertMessage = packet.ReadString();
        // pixelrp: the stock packet's third string was unused; the Room tool
        // puts the id of the room it is showing there, so "Send to Room"
        // reaches that room and not wherever the moderator happens to stand.
        // No id (an older client) keeps the old behaviour: the current room.
        var room = uint.TryParse(packet.ReadString(), out var roomId) && roomId > 0
            ? (_roomManager.TryGetRoom(roomId, out var target) ? target : null)
            : session.GetHabbo().CurrentRoom;
        // an unloaded room has nobody in it to read the alert
        if (room == null)
            return Task.CompletedTask;
        // pixelrp: room alerts render as the blue Information toast for everyone
        // in the room; the badge replaces the old "from Moderator" prefixes.
        room.SendPacket(new RoomNotificationComposer("room.alert",
            new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", alertMessage } }));
        return Task.CompletedTask;
    }
}
