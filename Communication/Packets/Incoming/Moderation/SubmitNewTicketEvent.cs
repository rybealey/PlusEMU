using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal class SubmitNewTicketEvent : IPacketEvent
{
    /// <summary>The most reported chat lines a single ticket will carry.</summary>
    private const int MaxReportedChats = 50;

    /// <summary>Smallest possible chat entry: int user id + the string's length header.</summary>
    private const int MinChatEntryLength = sizeof(int) + sizeof(ushort);

    public readonly IModerationManager _moderationManager;
    public readonly IGameClientManager _clientManager;
    public readonly IDatabase _database;

    public SubmitNewTicketEvent(IModerationManager moderationManager, IGameClientManager clientManager, IDatabase database)
    {
        _moderationManager = moderationManager;
        _clientManager = clientManager;
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Run a quick check to see if we have any existing tickets.
        if (_moderationManager.UserHasTickets(session.GetHabbo().Id))
        {
            var pendingTicket = _moderationManager.GetTicketBySenderId(session.GetHabbo().Id);
            if (pendingTicket != null)
            {
                session.Send(new CallForHelpPendingCallsComposer(pendingTicket));
                return Task.CompletedTask;
            }
        }
        var chats = new List<string>();
        var message = StringCharFilter.Escape(packet.ReadString().Trim());
        var category = packet.ReadInt();
        var reportedUserId = packet.ReadInt();
        var type = packet.ReadInt(); // Unsure on what this actually is.
        var reportedUser = PlusEnvironment.GetHabboById(reportedUserId);
        if (reportedUser == null)
        {
            // User doesn't exist.
            return Task.CompletedTask;
        }
        var messagecount = packet.ReadInt();

        // Bound the loop by a sane cap AND the bytes actually left. A client that
        // mis-encodes an earlier field shifts this read into garbage (a report sent
        // from a stale client bundle reads 65536 here), and running off the buffer
        // throws out of the handler — which silently loses the report, since the
        // ticket is only created below. Reading what is actually there instead
        // means such a report still reaches the mod tools.
        for (var i = 0; i < messagecount && i < MaxReportedChats; i++)
        {
            if (packet.Buffer.Length < MinChatEntryLength)
                break;
            packet.ReadInt();
            chats.Add(packet.ReadString());
        }
        // Id 0 is a placeholder — TryAddTicket replaces it with the row id.
        var ticket = new ModerationTicket(0, type, category, UnixTimestamp.GetNow(), 1, session.GetHabbo(), reportedUser, message, session.GetHabbo().CurrentRoom, chats);
        if (!_moderationManager.TryAddTicket(ticket))
            return Task.CompletedTask;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.RunQuery($"UPDATE `user_info` SET `cfhs` = `cfhs` + '1' WHERE `user_id` = '{session.GetHabbo().Id}' LIMIT 1");
        }
        _clientManager.ModAlert("A new support ticket has been submitted!");
        _clientManager.SendPacket(new ModeratorSupportTicketComposer(session.GetHabbo().Id, ticket), "mod_tool");
        return Task.CompletedTask;
    }
}