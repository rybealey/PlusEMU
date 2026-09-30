using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: the City tab - the held weather (-1 follows San
/// Francisco) and the live one, the pinned time of day (-1 follows the clock),
/// maintenance, the combat switch, and the Right now counts. `notice` is what
/// to tell the staff member about their last change.
/// </summary>
public class RpCityWorldComposer : IServerPacket
{
    private readonly CityWorld.State _state;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpCityWorldComposer;

    public RpCityWorldComposer(CityWorld.State state, string notice = "")
    {
        _state = state;
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_state.OverrideCode);
        packet.WriteInteger(_state.LiveCode);
        packet.WriteInteger(_state.PinnedMinutes);
        packet.WriteBoolean(_state.Maintenance);
        packet.WriteBoolean(_state.CombatPaused);
        packet.WriteInteger(_state.Online);
        packet.WriteInteger(_state.OnDuty);
        packet.WriteInteger(_state.Wanted);
        packet.WriteInteger(_state.Jailed);
        packet.WriteString(_notice);
    }
}
