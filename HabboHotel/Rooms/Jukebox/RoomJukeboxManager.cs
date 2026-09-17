using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.Jukebox;

// PixelRP: a room's jukebox. It owns the station - this room's queue, clock and
// now playing - plus the two things the room itself contributes: whether a
// jukebox stands in it at all, and that furni's play animation.
//
// The station used to be static and hotel-wide, and this class was a pass-
// through to it. Now there is one per room, and this is the only thing that
// should reach it: a packet handler finds the player's room and comes through
// here, which is what keeps one room's queue out of another's.
public class RoomJukeboxManager
{
    private readonly Room _room;
    private readonly JukeboxStation _station;

    public RoomJukeboxManager(Room room)
    {
        _room = room;
        _station = new JukeboxStation(room);
    }

    // Keyed on the BEHAVIOUR, not on the classname it used to hardcode
    // ("jukebox*1"). The music player is the hotel's own, so tying it to one
    // official furni meant a builder could not put it in a booth, a radio or a
    // custom cabinet - and the Function Tool already hands out behaviours.
    public static bool IsJukebox(Item item) =>
        item?.Definition?.InteractionType == InteractionType.Jukebox;

    public bool HasJukebox() => _room.GetRoomItemHandler().GetFloor.Any(IsJukebox);

    public static string ParseVideoId(string input) => JukeboxStation.ParseVideoId(input);

    public string TryAdd(GameClient session, string url) => _station.TryAdd(session, url);
    public void Enqueue(JukeboxTrack track) => _station.Enqueue(track);
    public bool TryRemove(GameClient session, int index) => _station.TryRemove(session, index);
    public bool TrySkip(GameClient session) => _station.TrySkip(session);
    public bool TryMove(GameClient session, int from, int to) => _station.TryMove(session, from, to);
    public void Report(GameClient session, int durationSec, bool ended) => _station.Report(session, durationSec, ended);

    // Room cycle: advance this room's station if due (guarded inside) and keep
    // its jukebox furni in step with what the room is playing.
    public void Cycle()
    {
        _station.Cycle();
        SyncJukeboxItemState();
    }

    // A jukebox arriving or leaving flips only this room's present flag.
    public void OnJukeboxPlaced() => BroadcastState();
    public void OnJukeboxRemoved() => BroadcastState();

    public IServerPacket BuildState() => _station.BuildState(HasJukebox());

    // The jukebox furni animates while THIS room's station plays (ExtraData "1")
    // and idles ("0") otherwise. Idempotent; sends only on a flip.
    public void SyncJukeboxItemState()
    {
        var extraData = _station.IsPlaying ? "1" : "0";
        foreach (var item in _room.GetRoomItemHandler().GetFloor.Where(IsJukebox).ToList())
        {
            if (item.ExtraData == null || item.ExtraData.Serialize() == extraData)
                continue;
            item.ExtraData.Store(extraData);
            item.UpdateState(false, true);
        }
    }

    public void BroadcastState()
    {
        _room.SendPacket(BuildState());
        SyncJukeboxItemState();
    }

    public void SendState(GameClient session) => session.Send(BuildState());
}
