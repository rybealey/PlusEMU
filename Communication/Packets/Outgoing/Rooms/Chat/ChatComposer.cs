using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

public class ChatComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly string _message;
    private readonly int _emotion;
    private readonly int _colour;
    private readonly string _usernameColor;
    public uint MessageId => ServerPacketHeader.ChatComposer;

    public ChatComposer(int virtualId, string message, int emotion, int colour, string usernameColor = "")
    {
        _virtualId = virtualId;
        _message = message;
        _emotion = emotion;
        _colour = colour;
        _usernameColor = usernameColor ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteString(_message);
        packet.WriteInteger(_emotion);
        packet.WriteInteger(_colour);
        packet.WriteInteger(0);
        packet.WriteInteger(-1);
        packet.WriteString(_usernameColor);
    }
}