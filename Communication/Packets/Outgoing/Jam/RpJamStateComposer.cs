using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Outgoing.Jam;

// PixelRP: everything one member needs to know about the jam they are in.
//
// Shaped like its older sibling RpJukeboxStateComposer: a presence flag, an
// optional current track with the clock beside it, then the queue. What a jam
// adds is who is here and who is in charge - a room never needed to say that,
// because you can see who is standing in it.
//
// IsHost is per recipient, so every member gets their own copy of this rather
// than one packet sent to a list.
public class RpJamStateComposer : IServerPacket
{
    private readonly bool _inJam;
    private readonly int _jamId;
    private readonly int _hostId;
    private readonly string _hostName;
    private readonly bool _isHost;
    private readonly bool _paused;
    private readonly List<JamMemberView> _members;
    private readonly JukeboxTrack _current;
    private readonly int _elapsedSec;
    private readonly List<JukeboxTrack> _queue;

    public uint MessageId => ServerPacketHeader.RpJamStateComposer;

    public RpJamStateComposer(int jamId, int hostId, string hostName, bool isHost, bool paused,
        List<JamMemberView> members, JukeboxTrack current, int elapsedSec, List<JukeboxTrack> queue)
    {
        _inJam = true;
        _jamId = jamId;
        _hostId = hostId;
        _hostName = hostName ?? "";
        _isHost = isHost;
        _paused = paused;
        _members = members ?? new List<JamMemberView>();
        _current = current;
        _elapsedSec = elapsedSec;
        _queue = queue ?? new List<JukeboxTrack>();
    }

    private RpJamStateComposer()
    {
        _inJam = false;
        _hostName = "";
        _members = new List<JamMemberView>();
        _queue = new List<JukeboxTrack>();
    }

    /// <summary>
    /// "You are in no jam." Sent rather than left unsaid: a client that has just
    /// reloaded cannot otherwise tell being in none apart from not having been
    /// told yet, and the difference decides whether it plays anything.
    /// </summary>
    public static RpJamStateComposer None() => new();

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_inJam);
        if (!_inJam)
            return;
        packet.WriteInteger(_jamId);
        packet.WriteInteger(_hostId);
        packet.WriteString(_hostName);
        packet.WriteBoolean(_isHost);
        packet.WriteBoolean(_paused);
        packet.WriteInteger(_members.Count);
        foreach (var member in _members)
        {
            packet.WriteInteger(member.Id);
            packet.WriteString(member.Username ?? "");
            packet.WriteBoolean(member.Away);
        }
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
