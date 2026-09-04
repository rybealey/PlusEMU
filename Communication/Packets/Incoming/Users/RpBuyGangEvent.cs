using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: create a gang from the Gang window - name plus two RAW RGB
/// colour ints (the Choose Your Looks palette picks; gangs bypass the
/// groups_items colour ids). A gang is a roomless group flagged is_gang.
/// Unlike stock PurchaseGroupEvent, every validation runs BEFORE credits
/// are deducted. All outcomes end in a fresh RpUserGangComposer for the
/// buyer; success broadcasts hotel-wide so open profiles update live.
/// </summary>
internal class RpBuyGangEvent : IPacketEvent
{
    private const int MaxNameLength = 29;

    // Placeholder badge: gang surfaces draw the split-colour crest from
    // colour1/colour2, never this code, but groups.badge is NOT NULL and the
    // stock badge renderer needs something parseable if a gang ever leaks
    // into a group surface.
    private const string DefaultBadge = "b0503X";

    private readonly IGroupManager _groupManager;
    private readonly IWordFilterManager _wordFilterManager;

    public RpBuyGangEvent(IGroupManager groupManager, IWordFilterManager wordFilterManager)
    {
        _groupManager = groupManager;
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var name = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var colourA = packet.ReadInt() & 0xFFFFFF;
        var colourB = packet.ReadInt() & 0xFFFFFF;

        void Refresh() => session.Send(GangUtility.ComposeFor(habbo.Id, GangUtility.GetGang(habbo.Id)));

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            Refresh();
            return Task.CompletedTask;
        }
        if (GangUtility.GetGang(habbo.Id) != null)
        {
            // already in a gang - the refresh flips their window to it
            Refresh();
            return Task.CompletedTask;
        }
        if (GangUtility.GangNameTaken(name))
        {
            session.Send(new BroadcastMessageAlertComposer($"A gang named '{name}' already exists - pick another name."));
            Refresh();
            return Task.CompletedTask;
        }

        var cost = GangUtility.GangCost();
        if (habbo.Credits < cost)
        {
            session.Send(new BroadcastMessageAlertComposer($"Founding a gang costs {cost} credits - you only have {habbo.Credits}."));
            Refresh();
            return Task.CompletedTask;
        }

        // roomId 0: gangs have no homeroom until turfs land - TryCreateGroup's
        // rooms/room_rights statements match nothing at id 0.
        if (!_groupManager.TryCreateGroup(habbo, name, "", 0, DefaultBadge, colourA, colourB, out var group))
        {
            Refresh();
            return Task.CompletedTask;
        }

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute("UPDATE `groups` SET `is_gang` = '1' WHERE `id` = @id", new { id = group.Id });
        }
        // roster sidecar: the founder's join date (roles come later from the Manage tab)
        GangManager.WriteMemberRow(group.Id, habbo.Id);

        // charge only after everything succeeded
        habbo.Credits -= cost;
        session.Send(new CreditBalanceComposer(habbo.Credits));

        GangUtility.BroadcastGangMembership(habbo.Id);
        return Task.CompletedTask;
    }
}
