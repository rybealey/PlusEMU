using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: outcome of a Clothing Store purchase. status 0 = bought
/// (unlocked = pieces now wearable, tokens = LTD tokens dropped in the
/// backpack), 1 = not enough credits, 2 = backpack full, 3 = an LTD sold out
/// while it sat on the mannequin, 4 = nothing to buy.
/// </summary>
public class RpBuyClothingResultComposer : IServerPacket
{
    public const int Ok = 0;
    public const int InsufficientCredits = 1;
    public const int BackpackFull = 2;
    public const int SoldOut = 3;
    public const int NothingToBuy = 4;

    private readonly int _status;
    private readonly int _unlocked;
    private readonly int _tokens;

    public uint MessageId => ServerPacketHeader.RpBuyClothingResultComposer;

    public RpBuyClothingResultComposer(int status, int unlocked = 0, int tokens = 0)
    {
        _status = status;
        _unlocked = unlocked;
        _tokens = tokens;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_status);
        packet.WriteInteger(_unlocked);
        packet.WriteInteger(_tokens);
    }
}
