using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Emergency Calls window (EmergencyCalls) - open with the newest
/// calls, newest first, or closed. Sent to one officer on clock-in and
/// clock-out, and to every on-duty officer whenever a call comes in, is
/// responded to or is marked. `notice` is what to tell this officer about
/// their own last press. A call's age is sent in seconds, not as a time, so a
/// client's clock cannot skew it. `reopen` (appended) brings the window back
/// up even if it was put away with its X - :calls.
///
/// After it, :backup's part (migration 251): for every call above, in the same
/// order, its kind and - for a backup request - who is responding. Appended
/// whole rather than threaded into each call, so a client from before it reads
/// the calls it expects and stops.
/// </summary>
public class RpEmergencyCallsComposer : IServerPacket
{
    private readonly bool _open;
    private readonly List<EmergencyCalls.Call> _calls;
    private readonly string _notice;
    private readonly bool _reopen;

    public uint MessageId => ServerPacketHeader.RpEmergencyCallsComposer;

    public RpEmergencyCallsComposer(bool open, List<EmergencyCalls.Call> calls, string notice = "", bool reopen = false)
    {
        _open = open;
        _calls = calls;
        _notice = notice ?? "";
        _reopen = reopen;
    }

    public void Compose(IOutgoingPacket packet)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        packet.WriteBoolean(_open);
        packet.WriteInteger(_calls.Count);
        foreach (var call in _calls)
        {
            packet.WriteInteger(call.Id);
            packet.WriteInteger(call.CallerId);
            packet.WriteString(call.CallerName);
            packet.WriteString(call.CallerLook);
            packet.WriteString(call.CallerGender);
            packet.WriteInteger(call.RoomId);
            packet.WriteString(call.RoomName);
            packet.WriteString(call.Message);
            packet.WriteInteger(Math.Max(0, now - call.CreatedAt));
            packet.WriteInteger(call.ResponderId);
            packet.WriteString(call.ResponderName);
            packet.WriteInteger(call.Mark);
            packet.WriteString(call.MarkedByName);
        }
        packet.WriteString(_notice);
        packet.WriteBoolean(_reopen);

        packet.WriteInteger(_calls.Count);
        foreach (var call in _calls)
        {
            packet.WriteInteger(call.Kind);
            packet.WriteInteger(call.Responders.Count);
            foreach (var responder in call.Responders)
            {
                packet.WriteInteger(responder.OfficerId);
                packet.WriteString(responder.OfficerName);
            }
        }
    }
}
