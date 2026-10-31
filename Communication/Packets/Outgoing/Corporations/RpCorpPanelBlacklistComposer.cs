using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: the Blacklist - every live entry, newest first:
/// the character it was added for, why, by whom, when, when it ends (0 =
/// never), and whether the viewer may remove it.
/// </summary>
public class RpCorpPanelBlacklistComposer : IServerPacket
{
    public record Entry(int Id, int UserId, string Username, string Look, string Gender, string Reason, string AddedByName, int CreatedAt, int ExpiresAt,
        bool CanRemove);

    private readonly List<Entry> _entries;

    public uint MessageId => ServerPacketHeader.RpCorpPanelBlacklistComposer;

    public RpCorpPanelBlacklistComposer(List<Entry> entries)
    {
        _entries = entries;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_entries.Count);
        foreach (var entry in _entries)
        {
            packet.WriteInteger(entry.Id);
            packet.WriteInteger(entry.UserId);
            packet.WriteString(entry.Username ?? "");
            packet.WriteString(entry.Look ?? "");
            packet.WriteString(entry.Gender ?? "");
            packet.WriteString(entry.Reason ?? "");
            packet.WriteString(entry.AddedByName ?? "");
            packet.WriteInteger(entry.CreatedAt);
            packet.WriteInteger(entry.ExpiresAt);
            packet.WriteBoolean(entry.CanRemove);
        }
    }
}
