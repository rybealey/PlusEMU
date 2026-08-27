using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Jukebox;

// PixelRP: broadcasts a room's jukebox state (now playing + queue) to clients.
// This repo composes outgoing packets via one IServerPacket class per message
// (see RpStatsComposer, GetYouTubeVideoComposer) rather than a generic
// ServerPacket(header) builder, so RoomJukeboxManager.BuildState() constructs
// this composer instead of writing fields directly.
public class RpJukeboxStateComposer : IServerPacket
{
    private readonly bool _hasJukebox;
    private readonly JukeboxTrack _current;
    private readonly int _elapsedSec;
    private readonly List<JukeboxTrack> _queue;

    public uint MessageId => ServerPacketHeader.RpJukeboxStateComposer;

    public RpJukeboxStateComposer(bool hasJukebox, JukeboxTrack current, int elapsedSec, List<JukeboxTrack> queue)
    {
        _hasJukebox = hasJukebox;
        _current = current;
        _elapsedSec = elapsedSec;
        _queue = queue;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_hasJukebox);
        packet.WriteBoolean(_current != null);
        if (_current != null)
        {
            packet.WriteString(_current.VideoId);
            packet.WriteString(_current.Title);
            packet.WriteString(_current.Author);
            packet.WriteInteger(_current.DurationSec);
            packet.WriteInteger(_elapsedSec);
            packet.WriteString(_current.QueuedBy);
        }
        packet.WriteInteger(_queue.Count);
        foreach (var track in _queue)
        {
            packet.WriteString(track.VideoId);
            packet.WriteString(track.Title);
            packet.WriteString(track.Author);
            packet.WriteString(track.QueuedBy);
        }
    }
}
