using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp: tells the client to open its floor plan editor for the room it is
/// in (the :floorplan staff command). No payload - the editor is opened
/// client-side and uses the floor data it already received on room entry.
/// </summary>
public class RpOpenFloorplanComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RpOpenFloorplanComposer;

    public void Compose(IOutgoingPacket packet)
    {
    }
}
