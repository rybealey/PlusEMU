using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Messenger;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Friends;

internal class MessengerEventSynchronizer : IAuthenticationTask
{
    private readonly IMessengerDataLoader _messengerDataLoader;
    private readonly IGameClientManager _gameClientManager;

    public MessengerEventSynchronizer(IMessengerDataLoader messengerDataLoader, IGameClientManager gameClientManager)
    {
        _messengerDataLoader = messengerDataLoader;
        _gameClientManager = gameClientManager;
    }

    public Task UserLoggedIn(Habbo habbo)
    {
        habbo.Messenger.FriendRequestUpdated += (_, args) => Guarded(() => OnFriendRequestUpdated(habbo, args));
        habbo.Messenger.FriendStatusUpdated += (_, args) => OnFriendStatusUpdated(habbo, args);
        habbo.Messenger.FriendUpdated += (_, args) => Guarded(() => OnFriendUpdated(habbo, args));
        habbo.Messenger.FriendsUpdated += (_, args) => Guarded(() => OnFriendsUpdated(habbo, args));
        habbo.Messenger.MessageSend += (_, args) => Guarded(() => OnMessageSend(habbo, args));
        habbo.Messenger.MessageReceived += (_, args) => OnMessageReceived(habbo, args);
        habbo.Messenger.RoomInviteReceived += (_, args) => OnRoomInviteReceived(habbo, args);
        habbo.Messenger.StatusUpdated += (_, _) => OnStatusUpdated(habbo);
        NotifyOnlineStatus(habbo);
        return Task.CompletedTask;
    }

    // pixelrp: these handlers run as async void, where nothing above them
    // catches a throw (e.g. the player logging out mid-message leaves
    // Client null), so each one logs its own errors.
    private static async void Guarded(Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception e)
        {
            Plus.Core.ExceptionLogger.LogException(e);
        }
    }

    private void OnStatusUpdated(Habbo habbo)
    {
        foreach (var friend in habbo.Messenger.Friends.Values)
        {
            var friendHabbo = friend.Habbo;
            var me = friendHabbo?.Messenger.GetFriend(habbo.Id);
            if (me == null) continue;
            friendHabbo?.Messenger.UpdateFriend(me);
        }
    }

    public Task UserLoggedOut(Habbo habbo)
    {
        NotifyOfflineStatus(habbo);
        return Task.CompletedTask;
    }


    private void OnRoomInviteReceived(Habbo habbo, MessengerMessageEventArgs args) => habbo.Client.Send(new RoomInviteComposer(args.Friend.Id, args.Message));

    private void OnMessageReceived(Habbo habbo, MessengerMessageEventArgs args) => habbo.Client.Send(new NewConsoleMessageComposer(args.Friend.Id, args.Message));

    private async Task OnMessageSend(Habbo habbo, MessengerMessageEventArgs args)
    {
        if (habbo.TimeMuted > 0)
        {
            habbo.Client.Send(new InstantMessageErrorComposer(MessengerMessageErrors.YourMuted, args.Friend.Id));
            return;
        }

        // pixelrp (Twist, 2026-10-27): logged beside the delivery, not before
        // it - a message waited for its chatlog row to be written before it was
        // sent on. Every message is still logged, delivered or not.
        var logged = _messengerDataLoader.LogPrivateMessage(habbo.Id, args.Friend.Id, args.Message);
        try
        {
            var target = _gameClientManager.GetClientByUserId(args.Friend.Id);
            if (target == null)
            {
                await _messengerDataLoader.LogPrivateOfflineMessage(habbo.Id, args.Friend.Id, args.Message);
                return;
            }

            if (target.GetHabbo().TimeMuted > 0)
            {
                habbo.Client.Send(new InstantMessageErrorComposer(MessengerMessageErrors.FriendMuted, args.Friend.Id));
                return;
            }

            if (!target.GetHabbo().AllowConsoleMessages || target.GetHabbo().IgnoresComponent.IsIgnored(habbo.Id))
            {
                habbo.Client.Send(new InstantMessageErrorComposer(MessengerMessageErrors.FriendBusy, args.Friend.Id));
                return;
            }

            // pixelrp: recipient has airplane mode on - don't deliver, and tell
            // the sender's phone the message bounced.
            if (target.GetHabbo().AirplaneMode)
            {
                habbo.Client.Send(new RpMessengerReceiptComposer(args.Friend.Id, RpMessengerReceiptComposer.NotDelivered));
                return;
            }

            var messenger = target.GetHabbo().Messenger;
            var friend = messenger.GetFriend(habbo.Id);
            if (friend == null) return;
            messenger.ReceiveMessage(friend, args.Message);
            // pixelrp: the friend's client got the message live - tell the
            // sender's phone it was delivered.
            habbo.Client.Send(new RpMessengerReceiptComposer(args.Friend.Id, RpMessengerReceiptComposer.Delivered));
        }
        finally
        {
            await logged;
        }
    }

    private async Task OnFriendsUpdated(Habbo habbo, MessengerBuddiesModifiedEventArgs args)
    {
        foreach (var (friend, change) in args.Changes)
        {
            if (change == BuddyModificationType.Removed)
                await RemoveFriend(habbo, friend);
        }
        habbo.Client.Send(new FriendListUpdateComposer(args.Changes));
    }

    private async Task OnFriendUpdated(Habbo habbo, MessengerBuddyModifiedEventArgs args)
    {
        if (args.BuddyModificationType == BuddyModificationType.Removed)
            await RemoveFriend(habbo, args.Buddy);
        habbo.Client.Send(new FriendListUpdateComposer(args.Buddy, args.BuddyModificationType));
    }

    private void OnFriendStatusUpdated(Habbo habbo, FriendStatusUpdatedEventArgs args) => habbo.Client.Send(new FriendNotificationComposer(args.Friend.Id, args.EventType, args.Value));

    private async Task OnFriendRequestUpdated(Habbo habbo, FriendRequestModifiedEventArgs args)
    {
        if (args.FriendRequestModificationType == FriendRequestModificationType.Accepted)
        {
            await _messengerDataLoader.DeleteFriendRequest(args.Request.FromId, habbo.Id);
            var (friend, me) = await _messengerDataLoader.CreateRelationship(args.Request.FromId, habbo.Id);
            me.Habbo = habbo;
            var from = _gameClientManager.GetClientByUserId(args.Request.FromId);
            if (from != null)
            {
                friend.Habbo = from.GetHabbo();
                from.GetHabbo().Messenger.AddFriend(me);
            }
            habbo.Messenger.AddFriend(friend);
        }
        else if (args.FriendRequestModificationType == FriendRequestModificationType.Declined)
        {
            await _messengerDataLoader.DeleteFriendRequest(args.Request.FromId, habbo.Id);
        }
        else if (args.FriendRequestModificationType == FriendRequestModificationType.Received)
        {
            habbo.Client.Send(new NewBuddyRequestComposer(args.Request.FromId, args.Request.Username, args.Request.Figure));
        }
        else if (args.FriendRequestModificationType == FriendRequestModificationType.Sent)
        {
            await _messengerDataLoader.RegisterFriendRequest(habbo.Id, args.Request.ToId);
            var target = _gameClientManager.GetClientByUserId(args.Request.ToId);
            if (target != null)
            {
                args.Request.FromId = habbo.Id;
                args.Request.Username = habbo.Username;
                args.Request.Figure = habbo.Look;
                target.GetHabbo().Messenger.AddFriendRequest(args.Request);
            }
        }
    }

    private async Task RemoveFriend(Habbo habbo, MessengerBuddy friend)
    {
        await _messengerDataLoader.DeleteFriendship(habbo.Id, friend.Id);
        var friendHabbo = _gameClientManager.GetClientByUserId(friend.Id);
        var habboBuddy = friendHabbo?.GetHabbo().Messenger.GetFriend(habbo.Id);
        if (habboBuddy != null)
            friendHabbo!.GetHabbo().Messenger.RemoveFriend(habboBuddy);
    }

    private void NotifyOnlineStatus(Habbo habbo)
    {
        foreach (var friend in habbo.Messenger.Friends)
        {
            var friendHabbo = _gameClientManager.GetClientByUserId(friend.Key);
            if (friendHabbo == null) continue;
            friend.Value.Habbo = friendHabbo.GetHabbo();
            var me = friendHabbo.GetHabbo().Messenger.GetFriend(habbo.Id);
            if (me == null) continue;
            me.Habbo = habbo;
            friendHabbo.GetHabbo().Messenger.UpdateFriend(me);
        }
    }

    private void NotifyOfflineStatus(Habbo habbo)
    {
        foreach (var friend in habbo.Messenger.Friends)
        {
            var friendHabbo = _gameClientManager.GetClientByUserId(friend.Key);
            if (friendHabbo == null) continue;
            friend.Value.Habbo = null;
            var me = friendHabbo.GetHabbo().Messenger.GetFriend(habbo.Id);
            if (me == null) continue;
            me.Habbo = null;
            friendHabbo.GetHabbo().Messenger.UpdateFriend(me);
        }
    }
}