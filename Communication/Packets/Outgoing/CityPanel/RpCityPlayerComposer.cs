using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: one player's card - sent when a player is opened, and
/// again after every action or backpack change so the panel shows what is true
/// now. `notice` is what to tell the staff member about the last change ("" for
/// nothing to say).
/// </summary>
public class RpCityPlayerComposer : IServerPacket
{
    private readonly CityPlayerCard _card;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpCityPlayerComposer;

    public RpCityPlayerComposer(CityPlayerCard card, string notice = "")
    {
        _card = card;
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_card.Id);
        packet.WriteString(_card.Username);
        packet.WriteString(_card.Look);
        packet.WriteString(_card.Gender);
        packet.WriteBoolean(_card.Online);
        packet.WriteString(_card.Where);
        packet.WriteInteger(_card.Health);
        packet.WriteInteger(_card.HealthMax);
        packet.WriteInteger(_card.Energy);
        packet.WriteInteger(_card.EnergyMax);
        packet.WriteInteger(_card.Aggression);
        packet.WriteInteger(_card.OpenCharges);
        packet.WriteInteger(_card.JailSecondsLeft);
        packet.WriteBoolean(_card.Cuffed);
        packet.WriteString(_card.Gang);
        packet.WriteString(_card.Job);
        packet.WriteBoolean(_card.OnDuty);
        packet.WriteInteger(_card.Credits);
        packet.WriteInteger(_card.UnlockedSlots);
        packet.WriteInteger(_card.Backpack.Count);
        foreach (var (slot, item, count) in _card.Backpack)
        {
            packet.WriteInteger(slot);
            packet.WriteString(item);
            packet.WriteInteger(count);
        }
        packet.WriteString(_notice);
    }
}
