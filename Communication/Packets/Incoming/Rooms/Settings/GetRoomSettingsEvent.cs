using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

internal class GetRoomSettingsEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public GetRoomSettingsEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        if (!_roomManager.TryLoadRoom(roomId, out var room))
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        session.Send(new RoomSettingsDataComposer(room));
        // pixelrp: the Roleplay tab's zone type rides alongside the stock
        // settings data (the stock packet's wire shape can't be extended).
        session.Send(new RpRoomZoneComposer(room.Id, room.IsSafeZone));
        // pixelrp: the Roleplay tab's HQ corp config (ranks + emergency
        // access flags) rides alongside the zone type for the same reason.
        session.Send(CorporationUtility.BuildRoomCorp(room));
        return Task.CompletedTask;
    }
}