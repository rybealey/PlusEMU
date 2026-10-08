using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the bank deposit box (DepositBox) - open with its contents (on
/// stepping onto one, and after every move), or closed (on stepping off).
/// `openSlots` is how many slots the player may use (16, or 20 with VIP);
/// `notice` is what to tell them about their last move ("" for nothing).
/// </summary>
public class RpDepositBoxComposer : IServerPacket
{
    private readonly bool _open;
    private readonly int _openSlots;
    private readonly List<(int Slot, string Item, int Count)> _items;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpDepositBoxComposer;

    public RpDepositBoxComposer(bool open, int openSlots, List<(int Slot, string Item, int Count)> items, string notice = "")
    {
        _open = open;
        _openSlots = openSlots;
        _items = items ?? new();
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        packet.WriteInteger(_openSlots);
        packet.WriteInteger(_items.Count);
        foreach (var (slot, item, count) in _items)
        {
            packet.WriteInteger(slot);
            packet.WriteString(item);
            packet.WriteInteger(count);
        }
        packet.WriteString(_notice);
    }
}
