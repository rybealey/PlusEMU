using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Corporations;

/// <summary>
/// pixelrp Corporation Panel: LOA and blacklist changes. Action, id, two ints
/// and two strings, unused ones 0 / "":
///   0 log LOA (id = employee, a = starts in days, b = days, text2 = reason)
///   1 approve LOA, 2 deny LOA, 3 end LOA (id = the LOA)
///   4 blacklist add (text1 = username, text2 = reason, a = days, 0 = for good)
///   5 blacklist remove (id = the entry)
///   6 blacklist check (text1 = username)
/// CorpRecords checks rank and corporation every time.
/// </summary>
internal class RpCorpPanelRecordEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Four ints and two strings at the least.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 4 * sizeof(int) + 2 * sizeof(short))
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var id = packet.ReadInt();
        var a = packet.ReadInt();
        var b = packet.ReadInt();
        var text1 = packet.ReadString();
        var text2 = packet.ReadString();
        CorpPanel.HandleRecord(session, action, id, a, b, text1, text2);
        return Task.CompletedTask;
    }
}
