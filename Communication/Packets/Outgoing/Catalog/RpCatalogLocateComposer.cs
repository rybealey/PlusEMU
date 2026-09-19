using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

public class RpCatalogLocateComposer : IServerPacket
{
    private readonly int _requestId, _pageId, _itemId, _status;
    public uint MessageId => ServerPacketHeader.RpCatalogLocateComposer;

    public RpCatalogLocateComposer(int requestId, int pageId, int itemId, int status)
    {
        _requestId = requestId;
        _pageId = pageId;
        _itemId = itemId;
        _status = status;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_requestId);
        packet.WriteInteger(_pageId);
        packet.WriteInteger(_itemId);
        packet.WriteInteger(_status);
    }
}
