namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>
/// pixelrp: the answer to a shop move (RpCatalogMoveFurniEvent), to the staff
/// member who asked. The category picker stays open until it lands, so a
/// refusal is shown where the choice was made rather than lost - the move is
/// silent otherwise, and a furni that did not go anywhere looks the same as one
/// that did until the page is opened again.
/// </summary>
public class RpCatalogMoveFurniComposer : IServerPacket
{
    private readonly bool _moved;
    private readonly int _catalogItemId;
    private readonly int _pageId;
    private readonly string _message;

    public uint MessageId => ServerPacketHeader.RpCatalogMoveFurniComposer;

    public RpCatalogMoveFurniComposer(bool moved, int catalogItemId, int pageId, string message)
    {
        _moved = moved;
        _catalogItemId = catalogItemId;
        _pageId = pageId;
        _message = message;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_moved);
        packet.WriteInteger(_catalogItemId);
        packet.WriteInteger(_pageId);
        packet.WriteString(_message ?? string.Empty);
    }
}
