using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp: the looks of everyone online, sent once at login (SSOTicketEvent).
/// The client downloads the clothes in them in the background, a few at a
/// time, so the people in the next room are drawn in their own clothes at
/// once instead of appearing a moment later. Looks only, never names;
/// duplicates sent once, and at most <see cref="MaxLooks"/>.
/// </summary>
public class RpPlayerLooksComposer : IServerPacket
{
    public const int MaxLooks = 150;

    private readonly ICollection<string> _looks;

    public uint MessageId => ServerPacketHeader.RpPlayerLooksComposer;

    public RpPlayerLooksComposer(ICollection<string> looks) => _looks = looks;

    /// <summary>Everyone online but the player logging in.</summary>
    public static RpPlayerLooksComposer ForLogin(GameClient session)
    {
        var ownId = session?.GetHabbo()?.Id ?? 0;
        var looks = new HashSet<string>();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (looks.Count >= MaxLooks)
                break;
            var habbo = client?.GetHabbo();
            if (habbo == null || habbo.Id == ownId || string.IsNullOrEmpty(habbo.Look))
                continue;
            looks.Add(habbo.Look);
        }
        return new RpPlayerLooksComposer(looks);
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_looks.Count);
        foreach (var look in _looks)
            packet.WriteString(look);
    }
}
