using Plus.Communication.Packets.Outgoing.Navigator.New;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Navigator;

namespace Plus.Communication.Packets.Incoming.Navigator;

internal class InitializeNewNavigatorEvent : IPacketEvent
{
    private readonly INavigatorManager _navigatorManager;

    public InitializeNewNavigatorEvent(INavigatorManager navigatorManager)
    {
        _navigatorManager = navigatorManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var topLevelItems = _navigatorManager.TopLevelItems;
        session.Send(new NavigatorMetaDataParserComposer(topLevelItems));
        session.Send(new NavigatorLiftedRoomsComposer());
        session.Send(new NavigatorCollapsedCategoriesComposer());
        session.Send(new NavigatorPreferencesComposer());
        return Task.CompletedTask;
    }
}