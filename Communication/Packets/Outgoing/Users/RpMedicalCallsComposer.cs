using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the hospital's emergency queue (MedicalCalls) - open with the
/// newest deaths, newest first, or closed. Sent to one paramedic on clock-in
/// and clock-out, and to every on-duty paramedic whenever somebody goes down.
/// A death's age is sent in seconds, not as a time, so a client's clock cannot
/// skew it. `reopen` brings the window back up even if it was put away with
/// its X - :calls.
/// </summary>
public class RpMedicalCallsComposer : IServerPacket
{
    private readonly bool _open;
    private readonly List<MedicalCalls.Death> _deaths;
    private readonly bool _reopen;

    public uint MessageId => ServerPacketHeader.RpMedicalCallsComposer;

    public RpMedicalCallsComposer(bool open, List<MedicalCalls.Death> deaths, bool reopen = false)
    {
        _open = open;
        _deaths = deaths;
        _reopen = reopen;
    }

    public void Compose(IOutgoingPacket packet)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        packet.WriteBoolean(_open);
        packet.WriteInteger(_deaths.Count);
        foreach (var death in _deaths)
        {
            packet.WriteInteger(death.Id);
            packet.WriteInteger(death.PatientId);
            packet.WriteString(death.PatientName);
            packet.WriteString(death.PatientLook);
            packet.WriteString(death.PatientGender);
            packet.WriteInteger(death.RoomId);
            packet.WriteString(death.RoomName);
            packet.WriteInteger(Math.Max(0, now - death.CreatedAt));
        }
        packet.WriteBoolean(_reopen);
    }
}
