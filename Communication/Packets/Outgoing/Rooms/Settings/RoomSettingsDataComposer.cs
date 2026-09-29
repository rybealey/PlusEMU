using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

public class RoomSettingsDataComposer : IServerPacket
{
    private readonly Room _room;

    public uint MessageId => ServerPacketHeader.RoomSettingsDataComposer;

    public RoomSettingsDataComposer(Room room)
    {
        _room = room;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(_room.RoomId);
        packet.WriteString(_room.Name);
        packet.WriteString(_room.Description);
        packet.WriteInteger(RoomAccessUtility.GetRoomAccessPacketNum(_room.Access));
        packet.WriteInteger(_room.Category);
        packet.WriteInteger(_room.UsersMax);
        packet.WriteInteger(RoomLimits.MaxVisitors); // pixelrp: the most the room may be set to hold
        packet.WriteInteger(_room.Tags.Count);
        foreach (var tag in _room.Tags.ToArray()) packet.WriteString(tag);
        packet.WriteInteger(_room.TradeSettings); //Trade
        packet.WriteInteger(_room.AllowPets ? 1 : 0); // allows pets in room - pet system lacking, so always off
        packet.WriteInteger(_room.AllowPetsEating ? 1 : 0); // allows pets to eat your food - pet system lacking, so always off
        packet.WriteInteger(_room.RoomBlockingEnabled ? 1 : 0);
        packet.WriteInteger(_room.Hidewall ? 1 : 0);
        packet.WriteInteger(_room.WallThickness);
        packet.WriteInteger(_room.FloorThickness);
        packet.WriteInteger(_room.ChatMode); //Chat mode
        packet.WriteInteger(_room.ChatSize); //Chat size
        packet.WriteInteger(_room.ChatSpeed); //Chat speed
        packet.WriteInteger(_room.ChatDistance); //Hearing Distance
        packet.WriteInteger(_room.ExtraFlood); //Additional Flood
        packet.WriteBoolean(true);
        packet.WriteInteger(_room.WhoCanMute); // who can mute
        packet.WriteInteger(_room.WhoCanKick); // who can kick
        packet.WriteInteger(_room.WhoCanBan); // who can ban

    }
}