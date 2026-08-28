using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.FriendList;

/// <summary>
/// pixelrp: the client is (or stopped) typing a message to a friend in the
/// phone's Messages app. Relay the typing state to that friend if they're
/// online. Live-only; both sides must still be friends for it to flow.
/// </summary>
internal class RpMessengerTypingEvent : IPacketEvent
{
    private readonly IGameClientManager _gameClientManager;

    public RpMessengerTypingEvent(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var friendId = packet.ReadInt();
        var typing = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null || habbo.Messenger.GetFriend(friendId) == null)
            return Task.CompletedTask;
        var target = _gameClientManager.GetClientByUserId(friendId);
        var targetHabbo = target?.GetHabbo();
        if (targetHabbo == null || targetHabbo.Messenger.GetFriend(habbo.Id) == null)
            return Task.CompletedTask;
        target!.Send(new RpMessengerFriendTypingComposer(habbo.Id, typing));
        return Task.CompletedTask;
    }
}
