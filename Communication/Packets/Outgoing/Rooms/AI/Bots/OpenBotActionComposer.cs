using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.AI.Bots;

public class OpenBotActionComposer : IServerPacket
{
    private readonly RoomUser _botUser;
    private readonly int _actionId;
    private readonly string _botSpeech;

    public uint MessageId => ServerPacketHeader.OpenBotActionComposer;

    public OpenBotActionComposer(RoomUser botUser, int actionId, string botSpeech)
    {
        _botUser = botUser;
        _actionId = actionId;
        _botSpeech = botSpeech;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_botUser.BotData.Id);
        packet.WriteInteger(_actionId);
        if (_actionId == 2)
            packet.WriteString(_botSpeech);
        else if (_actionId == 5)
            packet.WriteString(_botUser.BotData.Name);
        else if (_actionId == 3)
            // Current walk mode ("freeroam" / "stand") so the client can label
            // the toggle "Relax" vs "Walk Around".
            packet.WriteString(_botUser.BotData.WalkingMode);

    }
}