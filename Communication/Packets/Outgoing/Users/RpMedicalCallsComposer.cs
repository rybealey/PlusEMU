using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: Paramedic Dispatch (MedicalCalls) - open with this paramedic's
/// live call (offered to them or accepted by them; none once it is over), or
/// closed. Sent to one paramedic on clock-in and clock-out,
/// and to every on-duty paramedic whenever a call comes in, moves on to the
/// next paramedic, is claimed or closes. A death's age, and the seconds left
/// on its countdown - to accept it while offered, to retrieve the patient once
/// accepted - are sent as seconds rather than times, so a client's clock
/// cannot skew them. Each death carries its state (MedicalCalls.State*) and
/// who it is offered to, claimed by or revived by. `notice` is what to tell
/// this paramedic about their own claim, or the one they missed. `reopen`
/// brings the window back up even if it was put away with its X - :calls.
/// Last, appended: how long each countdown is in full, for the bar under the
/// call.
/// </summary>
public class RpMedicalCallsComposer : IServerPacket
{
    private readonly bool _open;
    private readonly List<MedicalCalls.Death> _deaths;
    private readonly string _notice;
    private readonly bool _reopen;

    public uint MessageId => ServerPacketHeader.RpMedicalCallsComposer;

    public RpMedicalCallsComposer(bool open, List<MedicalCalls.Death> deaths, string notice = "", bool reopen = false)
    {
        _open = open;
        _deaths = deaths;
        _notice = notice ?? "";
        _reopen = reopen;
    }

    public void Compose(IOutgoingPacket packet)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var utcNow = DateTime.UtcNow;
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
            packet.WriteInteger(death.State);
            packet.WriteInteger(death.ActorId);
            packet.WriteString(death.ActorName);
            packet.WriteInteger(death.State switch
            {
                MedicalCalls.StateOffered => SecondsUntil(death.OfferExpiresAt, utcNow),
                MedicalCalls.StateClaimed => SecondsUntil(death.ReviveBy, utcNow),
                _ => 0
            });
            // Was "this paramedic let it lapse", from when the window kept
            // history. Always false now; still written so the packet keeps the
            // layout the client reads.
            packet.WriteBoolean(false);
        }
        packet.WriteString(_notice);
        packet.WriteBoolean(_reopen);
        packet.WriteInteger(MedicalCalls.ClaimSeconds);
        packet.WriteInteger(MedicalCalls.ReviveSeconds);
    }

    private static int SecondsUntil(DateTime when, DateTime utcNow) =>
        Math.Max(0, (int)Math.Ceiling((when - utcNow).TotalSeconds));
}
