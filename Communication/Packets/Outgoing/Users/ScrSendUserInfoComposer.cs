using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Outgoing.Users;

// pixelrp: real subscription info for the purse HC chip and HC Center.
// Field order must match the client's UserSubscriptionParser exactly.
public class ScrSendUserInfoComposer : IServerPacket
{
    private readonly Habbo _habbo;
    private readonly int _responseType;

    public uint MessageId => ServerPacketHeader.ScrSendUserInfoComposer;

    public ScrSendUserInfoComposer(Habbo habbo, int responseType = 1)
    {
        _habbo = habbo;
        _responseType = responseType;
    }

    public void Compose(IOutgoingPacket packet)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var secondsLeft = Math.Max(0, _habbo.VipExpire - now);
        var daysLeft = (int)Math.Ceiling(secondsLeft / 86400.0);
        packet.WriteString("habbo_club");
        packet.WriteInteger(daysLeft);                       // daysToPeriodEnd
        packet.WriteInteger(_habbo.IsVip ? 1 : 0);           // memberPeriods
        packet.WriteInteger(0);                              // periodsSubscribedAhead
        packet.WriteInteger(_responseType);                  // 1 = login, 2 = purchase
        packet.WriteBoolean(_habbo.VipExpire > 0);           // hasEverBeenMember
        packet.WriteBoolean(_habbo.IsVip);                   // isVip
        packet.WriteInteger(0);                              // pastClubDays
        packet.WriteInteger(0);                              // pastVipDays
        packet.WriteInteger((int)Math.Min(int.MaxValue, secondsLeft / 60)); // minutesUntilExpiration
    }
}
