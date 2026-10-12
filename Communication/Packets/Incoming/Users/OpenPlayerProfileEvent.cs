using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;

namespace Plus.Communication.Packets.Incoming.Users;

internal class OpenPlayerProfileEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;
    private readonly IMessengerDataLoader _messengerDataLoader;
    private readonly IGameClientManager _gameClientManager;
    private readonly IUserDataFactory _userDataFactory;
    private readonly HabboStatsService _habboStatsService;

    public OpenPlayerProfileEvent(IGroupManager groupManager, IMessengerDataLoader messengerDataLoader, IGameClientManager gameClientManager, IUserDataFactory userDataFactory, HabboStatsService habboStatsService)
    {
        _groupManager = groupManager;
        _messengerDataLoader = messengerDataLoader;
        _gameClientManager = gameClientManager;
        _userDataFactory = userDataFactory;
        _habboStatsService = habboStatsService;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        packet.ReadBool(); //IsMe?

        // pixelrp: the hotel's own account (PixelRP, the owner of PixelRP
        // rooms) has no profile to show. Nothing in the client offers it.
        if (userId > 0 && userId == PixelRpRooms.AccountId)
            return;

        var targetData = _gameClientManager.GetClientByUserId(userId)?.GetHabbo();

        targetData ??= await _userDataFactory.GetUserDataByIdAsync(userId);

        if (targetData == null)
        {
            session.SendNotification("An error occurred whilst finding that user's profile.");
            return;
        }

        // Load HabboStats here
        var habboStats = await _habboStatsService.LoadHabboStats(userId);
        targetData.HabboStats = habboStats;

        var groups = _groupManager.GetGroupsForUser(targetData.Id);
        var friendCount = await _messengerDataLoader.GetFriendCount(userId);
        session.Send(new ProfileInformationComposer(targetData, session, groups, friendCount, habboStats));
    }
}
