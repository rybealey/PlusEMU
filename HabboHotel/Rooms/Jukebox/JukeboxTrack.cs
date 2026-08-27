namespace Plus.HabboHotel.Rooms.Jukebox;

public class JukeboxTrack
{
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public int DurationSec { get; set; } // 0 = unknown until a client reports it
    public string QueuedBy { get; set; }
    public int QueuedById { get; set; }
}
