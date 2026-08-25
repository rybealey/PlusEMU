using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.FriendList;

/// <summary>
/// pixelrp: the client opened (read) the phone conversation with a friend.
/// Relay a live read receipt to that friend if they're online; nothing is
/// persisted. Both sides must still be friends for the receipt to flow.
/// </summary>
internal class RpMessengerMarkReadEvent : IPacketEvent
{
    private readonly IGameClientManager _gameClientManager;

    public RpMessengerMarkReadEvent(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var friendId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || habbo.Messenger.GetFriend(friendId) == null)
            return Task.CompletedTask;
        var target = _gameClientManager.GetClientByUserId(friendId);
        var targetHabbo = target?.GetHabbo();
        if (targetHabbo == null || targetHabbo.Messenger.GetFriend(habbo.Id) == null)
            return Task.CompletedTask;
        target!.Send(new RpMessengerReceiptComposer(habbo.Id, RpMessengerReceiptComposer.Read));
        return Task.CompletedTask;
    }
}
