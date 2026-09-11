using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Privacy;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player changed something on the phone's Privacy screen.
///
/// The whole screen is sent every time rather than one field per packet - it
/// is four small numbers, and a partial update would need the server to know
/// which field the client meant. Echoed back so the screen renders what was
/// STORED (clamped) rather than what it asked for.
/// </summary>
internal class RpSavePrivacyEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var privacy = new PrivacyUtility.ProfilePrivacy(
            packet.ReadInt(),
            packet.ReadInt(),
            packet.ReadInt() == 1,
            packet.ReadInt() == 1);

        PrivacyUtility.Save(habbo.Id, privacy);
        session.Send(new RpPrivacyComposer(PrivacyUtility.Get(habbo.Id)));
        return Task.CompletedTask;
    }
}
