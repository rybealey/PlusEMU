using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Jukebox;

// PixelRP: the per-room face of the hotel-wide JukeboxStation. A room only
// contributes two things now - whether a jukebox stands in it (the room
// panel's present flag) and its jukebox furni's play animation. Queue,
// clock, rights and broadcasts live in the station.
public class RoomJukeboxManager
{
    private const string JukeboxItemName = "jukebox*1";

    private readonly Room _room;

    public RoomJukeboxManager(Room room)
    {
        _room = room;
    }

    public bool HasJukebox() =>
        _room.GetRoomItemHandler().GetFloor.Any(item => item.Definition.ItemName == JukeboxItemName);

    public static string ParseVideoId(string input) => JukeboxStation.ParseVideoId(input);

    public string TryAdd(GameClient session, string url) => JukeboxStation.TryAdd(session, url);
    public void Enqueue(JukeboxTrack track) => JukeboxStation.Enqueue(track);
    public bool TryRemove(GameClient session, int index) => JukeboxStation.TryRemove(session, index);
    public bool TrySkip(GameClient session) => JukeboxStation.TrySkip(session);
    public void Report(GameClient session, int durationSec, bool ended) => JukeboxStation.Report(session, durationSec, ended);

    // Room cycle: advance the station if due (guarded inside) and keep this
    // room's jukebox furni in step with the hotel's play state.
    public void Cycle()
    {
        JukeboxStation.Cycle();
        SyncJukeboxItemState();
    }

    // A jukebox arriving or leaving flips only this room's present flag.
    public void OnJukeboxPlaced() => BroadcastState();
    public void OnJukeboxRemoved() => BroadcastState();

    public IServerPacket BuildState() => JukeboxStation.BuildState(HasJukebox());

    // The jukebox furni animates while the station plays (ExtraData "1")
    // and idles ("0") otherwise. Idempotent; sends only on a flip.
    public void SyncJukeboxItemState()
    {
        var extraData = JukeboxStation.IsPlaying ? "1" : "0";
        foreach (var item in _room.GetRoomItemHandler().GetFloor.Where(item => item.Definition.ItemName == JukeboxItemName).ToList())
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
