using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Navigator;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Navigator.New;

public class NavigatorSearchResultSetComposer : IServerPacket
{
    private readonly string _category;
    private readonly string _data;
    private readonly ICollection<SearchResultList> _searchResultLists;
    private readonly GameClient _session;
    private readonly int _goBack;
    private readonly int _fetchLimit;

    /// <summary>pixelrp: every room this result set wrote, filled by Compose - their zones follow in RpNavigatorZonesComposer.</summary>
    public List<RoomData> Rooms { get; } = new();
    public uint MessageId => ServerPacketHeader.NavigatorSearchResultSetComposer;

    public NavigatorSearchResultSetComposer(string category, string data,
        ICollection<SearchResultList> searchResultLists, GameClient session, int goBack = 1, int fetchLimit = 12)
    {
        _category = category;
        _data = data;
        _searchResultLists = searchResultLists;
        _session = session;
        _goBack = goBack;
        _fetchLimit = fetchLimit;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_category); //Search code.
        packet.WriteString(_data); //Text?
        packet.WriteInteger(_searchResultLists.Count); //Count
        foreach (var searchResult in _searchResultLists.ToList())
        {
            packet.WriteString(searchResult.CategoryIdentifier);
            packet.WriteString(searchResult.PublicName);
            packet.WriteInteger(NavigatorSearchAllowanceUtility.GetIntegerValue(searchResult.SearchAllowance) != 0
                ? _goBack
                : NavigatorSearchAllowanceUtility.GetIntegerValue(searchResult
                    .SearchAllowance)); //0 = nothing, 1 = show more, 2 = back Action allowed.
            packet.WriteBoolean(false); //True = minimized, false = open.
            packet.WriteInteger(searchResult.ViewMode == NavigatorViewMode.Regular ? 0 :
                searchResult.ViewMode == NavigatorViewMode.Thumbnail ? 1 :
                0); //View mode, 0 = tiny/regular, 1 = thumbnail
            NavigatorHandler.Search(packet, searchResult, _data, _session, _fetchLimit, Rooms);
        }
    }
}