using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: rename the gang from its Settings tab (click the gang name).
/// Requires Administrator, which the owner always holds. The name follows the
/// founding rules (1 to 29 characters, word-filtered) and must not be taken by
/// another gang. Costs gang.rename.cost credits (100 by default), charged only
/// after every check has passed.
/// </summary>
internal class RpGangRenameEvent : IPacketEvent
{
    private const int MaxNameLength = 29;

    private readonly IGroupManager _groupManager;
    private readonly IWordFilterManager _wordFilterManager;

    public RpGangRenameEvent(IGroupManager groupManager, IWordFilterManager wordFilterManager)
    {
        _groupManager = groupManager;
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var name = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var actor = GangManager.GetActor(session, GangManager.PermAdmin);
        if (actor == null)
            return Task.CompletedTask;
        var habbo = session.GetHabbo();

        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            session.SendWhisper($"Gang names are 1 to {MaxNameLength} characters.");
            return Task.CompletedTask;
        }
        if (name == actor.Snapshot.Gang.Name)
        {
            session.SendWhisper("That's already your gang's name.");
            return Task.CompletedTask;
        }

        bool taken;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // another gang only - changing just the letter case of your own name is allowed
            taken = connection.QueryFirstOrDefault<int?>(
                "SELECT `id` FROM `groups` WHERE `is_gang` = '1' AND `name` = @name AND `id` <> @gangId LIMIT 1",
                new { name, gangId = actor.GangId }) != null;
        }
        if (taken)
        {
            session.SendNotification($"A gang named '{name}' already exists - pick another name.");
            return Task.CompletedTask;
        }

        var cost = GangManager.RenameCost();
        if (habbo.Credits < cost)
        {
            session.SendNotification($"Renaming your gang costs {TextHandling.GetMoney(cost)} - you only have {TextHandling.GetMoney(habbo.Credits)}.");
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `groups` SET `name` = @name WHERE `id` = @gangId AND `is_gang` = '1'", new { name, gangId = actor.GangId });
        }
        if (_groupManager.TryGetGroup(actor.GangId, out var group))
            group.Name = name;

        habbo.Credits -= cost;
        session.Send(new CreditBalanceComposer(habbo.Credits));
        session.SendWhisper($"Your gang is now called {name}.");

        GangManager.BroadcastDetail(actor.GangId);
        GangManager.BroadcastMembershipOfAll(actor.GangId);
        return Task.CompletedTask;
    }
}
