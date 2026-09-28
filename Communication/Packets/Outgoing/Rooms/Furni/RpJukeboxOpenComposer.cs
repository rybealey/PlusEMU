using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>
/// pixelrp: open the jukebox (Siri) for this player - the answer to
/// double-clicking any furni that has the jukebox behaviour.
///
/// The client already opens Siri itself for furni whose ART is a jukebox: the
/// renderer's FurnitureJukeboxLogic fires REQUEST_PLAYLIST_EDITOR. Art with any
/// other logic - a sound block, a plain multistate - sends an ordinary use
/// instead, so without this the behaviour was assigned and nothing opened.
/// No payload: which room's jukebox is the one the player is standing in.
/// </summary>
public class RpJukeboxOpenComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RpJukeboxOpenComposer;

    public void Compose(IOutgoingPacket packet)
    {
    }
}
