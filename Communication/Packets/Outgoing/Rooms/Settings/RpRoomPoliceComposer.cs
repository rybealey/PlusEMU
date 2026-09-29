using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp jail: the room's two police tags for the Room tool's Gameplay tab -
/// whether officers can :arrest here, and whether it is (part of) the jail.
/// Sent alongside the other Gameplay packets when the settings window opens,
/// and back to the staff member who flips either switch (RpSetEmergencyEvent,
/// categories 3 and 4). A packet of its own rather than two more fields on
/// RpRoomCorpComposer, whose parser lives in the patched renderer; this one's
/// is in client source (RpJailMessages.ts).
/// </summary>
public class RpRoomPoliceComposer : IServerPacket
{
    private readonly int _roomId;
    private readonly bool _isArrestRoom;
    private readonly bool _isJailRoom;

    public uint MessageId => ServerPacketHeader.RpRoomPoliceComposer;

    public RpRoomPoliceComposer(uint roomId, bool isArrestRoom, bool isJailRoom)
    {
        _roomId = (int)roomId;
        _isArrestRoom = isArrestRoom;
        _isJailRoom = isJailRoom;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteBoolean(_isArrestRoom);
        packet.WriteBoolean(_isJailRoom);
    }
}
