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
///
/// It also carries whether this player holds `rp_furni_function`, the command
/// permission the Function tool's own two packets check. The client had been
/// approximating that with rank, which is not the same question: a rank can be
/// high and hold no such row, and the button then opened a panel the server
/// refused. The permission does not change while somebody is online, so riding
/// the duty push is enough - it already fires at login.
/// </summary>
public class RpStaffDutyComposer : IServerPacket
{
    private readonly bool _onDuty;
    private readonly bool _canFurniFunction;

    public uint MessageId => ServerPacketHeader.RpStaffDutyComposer;

    public RpStaffDutyComposer(bool onDuty, bool canFurniFunction)
    {
        _onDuty = onDuty;
        _canFurniFunction = canFurniFunction;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_onDuty);
        packet.WriteBoolean(_canFurniFunction);
    }
}
