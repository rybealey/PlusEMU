using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>
/// pixelrp: a player's favourite shop categories, in the order they were
/// starred - a count, then that many page ids. Sent whole every time, in answer
/// to a request and after every star, so the client never has to merge.
/// </summary>
public class RpCatalogFavouritesComposer : IServerPacket
{
    private readonly IReadOnlyList<int> _pageIds;

    public uint MessageId => ServerPacketHeader.RpCatalogFavouritesComposer;

    public RpCatalogFavouritesComposer(IReadOnlyList<int> pageIds)
    {
        _pageIds = pageIds;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_pageIds.Count);
        foreach (var pageId in _pageIds)
            packet.WriteInteger(pageId);
    }
}
