using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: invite somebody to your jam. It arrives as a message in their
// console, where the client draws it as a card with a Join on it.
//
// ANYONE, not just friends. The stock room invite refuses a stranger outright
// (SendRoomInviteEvent checks FriendshipExists and drops the rest silently),
// and that check is deliberately not copied here - a jam is a thing you invite
// people to, and needing to be friends first makes it a thing you invite your
// existing friends to.
//
// Which opens a door the hotel did not previously have: a message from someone
// you have never met. Three things hold it closed enough.
//   - The recipient's own console setting still decides. Somebody who has
//     turned messages off is not reachable this way either.
//   - A cooldown per PAIR, so the same person cannot be invited over and over.
//   - A cap on how many invites one jam can send in a session, so a script
//     cannot walk the user table.
// Muted players cannot send at all, on the same reasoning the room invite uses.
internal class RpJamInviteEvent : IPacketEvent
{
    private const int InviteCooldownSec = 60;
    private const int MaxInvitesPerJam = 50;

    // (senderId, targetId) -> when. Static: the limit is on the person, and
    // outlives any one jam they start.
    private static readonly ConcurrentDictionary<(int, int), DateTime> _lastInvite = new();
    private static readonly ConcurrentDictionary<int, int> _invitesSent = new();

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var targetId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        if (habbo.TimeMuted > 0)
        {
            session.SendNotification("You're muted - you can't send jam invites right now.");
            return Task.CompletedTask;
        }
        var jam = JamManager.GetFor(habbo.Id);
        if (jam == null)
        {
            session.SendNotification("Start a jam before inviting anyone to it.");
            return Task.CompletedTask;
        }
        if (targetId == habbo.Id)
            return Task.CompletedTask;
        if (jam.Has(targetId))
        {
            session.SendNotification("They're already in your jam.");
            return Task.CompletedTask;
        }
        if (_invitesSent.GetValueOrDefault(jam.Id) >= MaxInvitesPerJam)
        {
            session.SendNotification("That's enough invites for one jam.");
            return Task.CompletedTask;
        }
        var key = (habbo.Id, targetId);
        if (_lastInvite.TryGetValue(key, out var last) && (DateTime.UtcNow - last).TotalSeconds < InviteCooldownSec)
        {
            session.SendNotification("You've already invited them - give them a moment.");
            return Task.CompletedTask;
        }
        var target = PlusEnvironment.Game.ClientManager.GetClientByUserId(targetId);
        // Offline is not an error worth naming precisely. A jam is happening
        // now, so an invite to somebody who is not here has nothing to offer
        // them - and saying "they are offline" would report on who is online to
        // anyone willing to try user ids.
        if (target?.GetHabbo() == null || !target.GetHabbo().AllowConsoleMessages)
        {
            session.SendNotification("They can't be invited right now.");
            return Task.CompletedTask;
        }
        _lastInvite[key] = DateTime.UtcNow;
        _invitesSent.AddOrUpdate(jam.Id, 1, (_, count) => count + 1);
        // The marker is what tells the client to draw a Join card instead of a
        // line of text. A client that does not know the marker still shows
        // something readable, which is the point of putting the words in it.
        target.Send(new NewConsoleMessageComposer(habbo.Id,
            $"[jam:{jam.Id}] {habbo.Username} invited you to their jam session."));
        session.SendNotification($"Invited {target.GetHabbo().Username} to your jam.");
        return Task.CompletedTask;
    }
}
