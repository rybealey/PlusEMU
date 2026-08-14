using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Moderation;

public class ModerationTicket
{
    public List<string> ReportedChats;

    /// <summary>A freshly submitted report, taken from the live session objects.</summary>
    public ModerationTicket(int id, int type, int category, double timestamp, int priority, Habbo? sender, Habbo? reported, string issue, RoomData? room,
        List<string> reportedChats)
        : this(id, type, category, timestamp, priority,
            sender?.Id ?? 0, sender?.Username ?? string.Empty,
            reported?.Id ?? 0, reported?.Username ?? string.Empty,
            0, string.Empty, issue, room?.Id ?? 0, room?.Name ?? string.Empty, reportedChats)
    {
    }

    /// <summary>
    /// A ticket rebuilt from its `moderation_tickets` row. Ids and usernames are
    /// carried as plain values because after a restart the reporter, the reported
    /// user and the picking moderator are usually all offline, and
    /// PlusEnvironment.GetHabboById only ever resolves users who are online.
    /// </summary>
    public ModerationTicket(int id, int type, int category, double timestamp, int priority, int senderId, string senderUsername, int reportedId,
        string reportedUsername, int moderatorId, string moderatorUsername, string issue, uint roomId, string roomName, List<string>? reportedChats)
    {
        Id = id;
        Type = type;
        Category = category;
        Timestamp = timestamp;
        Priority = priority;
        SenderId = senderId;
        SenderUsername = senderUsername;
        ReportedId = reportedId;
        ReportedUsername = reportedUsername;
        ModeratorId = moderatorId;
        ModeratorUsername = moderatorUsername;
        Issue = issue;
        RoomId = roomId;
        RoomName = roomName;
        Answered = false;
        ReportedChats = reportedChats ?? new();
    }

    public int Id { get; set; }
    public int Type { get; set; }
    public int Category { get; set; }
    public double Timestamp { get; set; }
    public int Priority { get; set; }
    public bool Answered { get; set; }
    public int SenderId { get; set; }
    public string SenderUsername { get; set; }
    public int ReportedId { get; set; }
    public string ReportedUsername { get; set; }

    /// <summary>The moderator who picked this ticket, or 0 while it is unpicked.</summary>
    public int ModeratorId { get; set; }

    public string ModeratorUsername { get; set; }
    public string Issue { get; set; }

    /// <summary>The room the report was made in, or 0 if the reporter was not in one.</summary>
    public uint RoomId { get; set; }

    public string RoomName { get; set; }

    /// <summary>The ticket's tab, as seen by the moderator whose id is passed in.</summary>
    public int GetStatus(int id)
    {
        if (ModeratorId == 0)
            return 1;
        if (ModeratorId == id && !Answered)
            return 2;
        return 3;
    }
}
