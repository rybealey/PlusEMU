using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: whether this player currently holds their global room rights.
///
/// Rank alone used to be the client's answer. The emulator only honours
/// `room_any_owner` / `room_any_rights` while their holder is clocked in at
/// City Government, but the CLIENT opens its furni tools on `isModerator`,
/// which is rank and nothing else - so off duty it went on offering moves,
/// pickups and state toggles that the server then refused. A button that
/// does nothing is worse than no button.
///
/// Sent at login and at every moment the answer changes - the same four
/// clock events the pardon-rights push already rides. It gates the
/// AFFORDANCE only; every packet re-checks server-side regardless.
/// </summary>
public class RpStaffDutyComposer : IServerPacket
{
    private readonly bool _onDuty;

    public uint MessageId => ServerPacketHeader.RpStaffDutyComposer;

    public RpStaffDutyComposer(bool onDuty)
    {
        _onDuty = onDuty;
    }

    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(_onDuty);
}
