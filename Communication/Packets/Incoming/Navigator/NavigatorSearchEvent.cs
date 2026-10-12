using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Navigator.New;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Navigator;

namespace Plus.Communication.Packets.Incoming.Navigator;

[StaffOnly]
internal class NavigatorSearchEvent : IPacketEvent
{
    private readonly INavigatorManager _navigatorManager;

    public NavigatorSearchEvent(INavigatorManager navigatorManager)
    {
        _navigatorManager = navigatorManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var category = packet.ReadString();
        var search = packet.ReadString();
        ICollection<SearchResultList> categories = new List<SearchResultList>();
        if (!string.IsNullOrEmpty(search))
        {
            if (_navigatorManager.TryGetSearchResultList(0, out var queryResult)) categories.Add(queryResult);
        }
        else if (category == "official_view")
        {
            // pixelrp: the Public tab is the PixelRP rooms, a list per room
            // category, Corporations first (NavigatorManager).
            categories = _navigatorManager.GetPixelRpPublicLists();
        }
        else
        {
            categories = _navigatorManager.GetCategoriessForSearch(category);
            if (categories.Count == 0)
            {
                //Are we going in deep?!
                categories = _navigatorManager.GetResultByIdentifier(category).ToList();
                // pixelrp: or into one of the Public tab's lists ("show more")
                if (categories.Count == 0 && category.StartsWith(NavigatorManager.PixelRpListPrefix))
                    categories = _navigatorManager.GetPixelRpPublicLists(category);
                if (categories.Count > 0)
                {
                    var deep = new NavigatorSearchResultSetComposer(category, search, categories, session, 2, 100);
                    session.Send(deep);
                    session.Send(new RpNavigatorZonesComposer(deep.Rooms));
                    return Task.CompletedTask;
                }
            }
        }
        var results = new NavigatorSearchResultSetComposer(category, search, categories, session);
        session.Send(results);
        // pixelrp: the listed rooms' zones, for the navigator's SAFE / UNSAFE / TURF tags
        // (Send composes at once, so Rooms is filled by now)
        session.Send(new RpNavigatorZonesComposer(results.Rooms));
        return Task.CompletedTask;
    }
}