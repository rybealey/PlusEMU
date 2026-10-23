using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: a room's floor opacity - how see-through its floor tiles are, 0
/// (gone) to 100 (solid), kept on rooms.floor_opacity (306_FloorOpacity).
/// Staff set it with :topacity in the room they stand in, or with the Room
/// tool's slider for any room (RpSetFloorOpacityEvent).
///
/// Only the picture changes: the client fades the floor planes, and a floor at
/// 0 is still clicked and walked on, so nothing here touches the room's map.
/// A player is sent it straight behind the room's shape on walking in
/// (RoomEntry), and the whole room is sent it again when it changes.
/// </summary>
public static class RoomFloorOpacity
{
    /// <summary>Every room's opacity until staff change it.</summary>
    public const int Solid = 100;

    /// <summary>
    /// Stores the opacity, held to 0-100, and shows everyone in the room.
    /// Returns what it was set to.
    /// </summary>
    public static int Set(Room room, int opacity)
    {
        opacity = Math.Clamp(opacity, 0, Solid);
        if (opacity == room.FloorOpacity)
            return opacity;
        room.FloorOpacity = opacity;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `rooms` SET `floor_opacity` = @opacity WHERE `id` = @roomId LIMIT 1",
                new { opacity, roomId = room.Id });
        }
        room.SendPacket(new RpRoomFloorOpacityComposer(room.Id, opacity));
        return opacity;
    }
}
