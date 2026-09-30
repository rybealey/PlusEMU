using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp jail: the room's police tag for the Room tool's Gameplay tab -
/// whether it is (part of) the jail. Sent alongside the other Gameplay packets
/// when the settings window opens, and back to the staff member who flips the
/// switch (RpSetEmergencyEvent, category 4). A packet of its own rather than a
/// field on RpRoomCorpComposer, whose parser lives in the patched renderer;
/// this one's is in client source (RpJailMessages.ts).
///
/// It carried an arrest room flag before the jail's; that went with the tag,
/// now an arrest_point furni behaviour (ArrestCommand).
/// </summary>
public class RpRoomPoliceComposer : IServerPacket
{
    private readonly int _roomId;
    private readonly bool _isJailRoom;

    public uint MessageId => ServerPacketHeader.RpRoomPoliceComposer;

    public RpRoomPoliceComposer(uint roomId, bool isJailRoom)
    {
        _roomId = (int)roomId;
        _isJailRoom = isJailRoom;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteBoolean(_isJailRoom);
    }
}
