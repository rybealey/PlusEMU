using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: pushes the player's saved macros to their client at login, as the
/// single JSON document described in 62_Macros.sql. An empty string means
/// nothing has ever been saved, which the client reads as "use my defaults" -
/// it is deliberately not an empty JSON object, so the client can tell a fresh
/// account apart from one that has deliberately cleared every macro.
/// </summary>
public class RpMacrosComposer : IServerPacket
{
    private readonly string _macros;

    public uint MessageId => ServerPacketHeader.RpMacrosComposer;

    public RpMacrosComposer(string macros)
    {
        _macros = macros ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_macros);
    }
}
