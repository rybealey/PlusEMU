using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Emergency Calls window (EmergencyCalls) - open with the newest
/// calls, newest first, or closed. Sent to one officer on clock-in and
/// clock-out, and to every on-duty officer whenever a call comes in, is
/// responded to or is marked. `notice` is what to tell this officer about
/// their own last press. A call's age is sent in seconds, not as a time, so a
/// client's clock cannot skew it.
/// </summary>
public class RpEmergencyCallsComposer : IServerPacket
{
    private readonly bool _open;
    private readonly List<EmergencyCalls.Call> _calls;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpEmergencyCallsComposer;

    public RpEmergencyCallsComposer(bool open, List<EmergencyCalls.Call> calls, string notice = "")
    {
        _open = open;
        _calls = calls;
        _notice = notice ?? "";
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
    }
}
