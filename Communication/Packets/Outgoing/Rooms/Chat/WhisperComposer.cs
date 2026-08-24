using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

public class WhisperComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly string _text;
    private readonly int _emotion;
    private readonly int _colour;
    private readonly string _usernameColor;
    private readonly string _icon;
    private readonly string _iconColor;

    public uint MessageId => ServerPacketHeader.WhisperComposer;

    public WhisperComposer(int virtualId, string text, int emotion, int colour, string usernameColor = "", string icon = "", string iconColor = "")
    {
        _virtualId = virtualId;
        _text = text;
        _emotion = emotion;
        _colour = colour;
        _usernameColor = usernameColor ?? "";
        _icon = icon ?? "";
        _iconColor = iconColor ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteString(_text);
        packet.WriteInteger(_emotion);
        packet.WriteInteger(_colour);
        packet.WriteInteger(0);
        packet.WriteInteger(-1);
        packet.WriteString(_usernameColor);
        packet.WriteString(_icon);
        packet.WriteString(_iconColor);
    }
}