using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms;

internal static class RoomAppender
{
    public static void WriteRoom(IOutgoingPacket packet, RoomData data)
    {
        packet.WriteUInteger(data.Id);
        packet.WriteString(data.Name);
        packet.WriteInteger(data.OwnerId);
        packet.WriteString(data.OwnerName);
        packet.WriteInteger(RoomAccessUtility.GetRoomAccessPacketNum(data.Access));
        packet.WriteInteger(data.UsersNow);
        packet.WriteInteger(data.UsersMax);
        packet.WriteString(data.Description);
        packet.WriteInteger(data.TradeSettings);
        packet.WriteInteger(data.Score);
        packet.WriteInteger(0); //Top rated room rank.
        packet.WriteInteger(data.Category);
        packet.WriteInteger(data.Tags.Count);
        foreach (var tag in data.Tags) packet.WriteString(tag);
        var roomType = 0;
        if (data.Group != null)
            roomType += 2;
        // (4 was "has a promotion" - promoted rooms are gone, so it is never set
        // and the promotion fields below it are never written)
        if (data.Type == "private")
            roomType += 8;
        if (data.AllowPets)
            roomType += 16;
        if (PlusEnvironment.Game.Navigator.TryGetFeaturedRoom(data.Id, out var item)) roomType += 1;
        packet.WriteInteger(roomType);
        if (item != null) packet.WriteString(item.Image);
        if (data.Group != null)
        {
            packet.WriteInteger(data.Group?.Id ?? 0);
            packet.WriteString(data.Group == null ? "" : data.Group.Name);
            packet.WriteString(data.Group == null ? "" : data.Group.Badge);
        }
    }
}