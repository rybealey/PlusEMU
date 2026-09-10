using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: tells one client whether it may drop charges from the Wanted list.
///
/// Per-recipient, unlike the wanted list itself, which is one broadcast every
/// client reads the same way. The answer is "are you an on-duty officer right
/// now", so it is sent at login and again on every clock-in and clock-off -
/// the only moments it can change.
///
/// It gates the AFFORDANCE only. The x on a charge is hidden for everyone else
/// rather than shown and refused, but the refusal is still there: the drop
/// packet re-checks the same rule server-side, because a client is free to
/// send anything.
/// </summary>
public class RpPoliceComposer : IServerPacket
{
    private readonly bool _canPardon;

    public uint MessageId => ServerPacketHeader.RpPoliceComposer;

    public RpPoliceComposer(bool canPardon)
    {
        _canPardon = canPardon;
    }

    // An int, not WriteBoolean: every other pixelrp packet sends its flags as
    // 1/0 ints and the client reads them with readInt() === 1.
    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(_canPardon ? 1 : 0);
}
