using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :heal &lt;player&gt;. One word, two entirely different acts.
///
/// A CLOCKED-IN HOSPITAL EMPLOYEE is selling a service. It goes through the
/// offer path - a card the other player answers, a syringe in the medic's
/// hand, 70% of the bar at once and the rest on a drip. That is the hospital's
/// treatment and it is meant to be the good one.
///
/// EVERYBODY ELSE is doing first aid out of their own backpack. A medkit is
/// spent, it works exactly as a medkit does when you use it on yourself - the
/// bar refills over a minute, with no instant jab - and it only reaches people
/// you already have a reason to be patching up: your own gang, or, for an
/// on-duty officer, anyone who is not in cuffs (their own prisoner included).
///
/// The second path does NOT ask permission, and that is deliberate. The card
/// exists because a SALE has a price and a price needs consent; a medkit out
/// of your own pocket costs the recipient nothing, and the two people it is
/// available to are a gangmate mid-fight and a prisoner who may be in no
/// position to answer anything. A confirmation dialogue over a battlefield
/// heal is a confirmation nobody reads.
/// </summary>
internal class HealCommand : ITargetChatCommand
{
    public string Key => "heal";

    public string PermissionRequired => "command_heal";

    public string Parameters => "%target%";

    public string Description => "Treat another player, or offer hospital care.";

    public bool MustBeInSameRoom => true;

    public bool IsRanged => true;

    public string NoTargetMessage => "Heal who? :heal <player>";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || target == null || room == null)
            return Task.CompletedTask;

        // The hospital first: on the clock, it is the paid service and goes
        // through the card.
        if (MedicalUtility.IsOnDutyHospitalStaff(habbo.Id))
            return OfferCommand.Place(session, room, target, "heal", 1);

        return FirstAid(session, room, habbo, target);
    }

    /// <summary>
    /// A medkit, out of your own backpack, into somebody you are entitled to
    /// patch up.
    ///
    /// ORDER MATTERS AND IS NOT INTERCHANGEABLE. Permission, then reach, then
    /// their condition, and the medkit last - so nothing is ever spent on a
    /// heal that some earlier rule was going to refuse anyway. The item comes
    /// out of the bag on the line before the bar starts filling and not a
    /// moment sooner.
    /// </summary>
    private static Task FirstAid(GameClient session, Room room, Habbo habbo, Habbo target)
    {
        if (!Entitled(habbo, target))
        {
            session.SendWhisper(
                MedicalUtility.IsHospitalStaff(habbo.Id) ? "You have to be on duty to treat somebody."
                : PoliceUtility.CanActAsOfficer(habbo.Id) ? "You can't patch up somebody who's in cuffs."
                : "You can only patch up your own gang, or somebody you have in custody.");
            return Task.CompletedTask;
        }

        var manager = room.GetRoomUserManager();
        var healer = manager?.GetRoomUserByHabbo(habbo.Id);
        var patient = manager?.GetRoomUserByHabbo(target.Id);
        if (healer == null || patient == null)
        {
            session.SendWhisper(RangeMessages.NotInRoom);
            return Task.CompletedTask;
        }

        // The same reach :hit and :slap use: your own tile plus the four
        // sharing an edge with it, no diagonals. A medkit is something you
        // press against somebody, so it reaches exactly as far as a punch.
        if ((Math.Abs(patient.X - healer.X) + Math.Abs(patient.Y - healer.Y)) > 1)
        {
            session.SendWhisper(RangeMessages.TooFar(target.Username));
            return Task.CompletedTask;
        }

        target.EnsureRpStatsLoaded();

        // Out cold is not an injury a friend can patch. It is undone by being
        // revived, which is the paramedics' whole job - and a gang that could
        // pick its own people up off the floor would make the ambulance
        // pointless.
        if (target.RpHealth <= 0)
        {
            session.SendWhisper(KnockedOut.TargetRefusal);
            return Task.CompletedTask;
        }
        if (target.RpHealth >= target.RpHealthMax)
        {
            session.SendWhisper($"{target.Username} is full health.");
            return Task.CompletedTask;
        }
        if (target.RpHealthRegen.Running)
        {
            session.SendWhisper($"{target.Username} is already being patched up.");
            return Task.CompletedTask;
        }

        // pixelrp trading: the backpack is frozen while a trade is open.
        if (Plus.HabboHotel.Rooms.Trading.RpTrades.IsTrading(habbo.Id))
        {
            session.SendWhisper(Plus.HabboHotel.Rooms.Trading.RpTrades.FrozenMessage);
            return Task.CompletedTask;
        }

        // A Cop Medkit heals the same as a medkit. It goes first: the police
        // locker replaces it for nothing, and a medkit costs money.
        var inventory = habbo.LoadRpInventory();
        var slot = inventory.FirstOrDefault(entry => entry.Item == PoliceUtility.CopMedkitItem).Slot;
        if (slot <= 0)
            slot = inventory.FirstOrDefault(entry => entry.Item == "medkit").Slot;
        if (slot <= 0)
        {
            session.SendWhisper("You need a medkit in your backpack to do that.");
            return Task.CompletedTask;
        }

        habbo.ConsumeRpItem(slot);
        session.Send(new Communication.Packets.Outgoing.Users.RpInventoryComposer(habbo.LoadRpInventory()));
        target.RpHealthRegen.Start(target.RpHealthMax);

        // Bubble 4, the one a medkit already speaks in when it is used on
        // yourself - this is the same act with somebody else on the end of it.
        // An on-duty officer's heal is a police action, and takes the badge
        // bubble (PoliceUtility.ActionBubbleFor).
        healer.OnChat(PoliceUtility.ActionBubbleFor(healer.HabboId), $"*opens a medkit and patches {target.Username} up*", true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether these two have any business doing first aid on each other.
    ///
    /// Two ways in: the same gang, or an on-duty officer treating anyone who
    /// is not in cuffs - plus their own escorted prisoner, who is. A cuffed
    /// person is otherwise off limits; they are somebody's business already.
    /// </summary>
    private static bool Entitled(Habbo habbo, Habbo target)
    {
        var mine = GangUtility.GetGangShown(habbo.Id);
        var theirs = GangUtility.GetGangShown(target.Id);
        if (mine != null && theirs != null && mine.GangId != 0 && mine.GangId == theirs.GangId)
            return true;

        // An on-duty officer can patch up anyone who is NOT in cuffs, and
        // still their own prisoner even though that person is cuffed.
        return PoliceUtility.CanActAsOfficer(habbo.Id)
            && (!PoliceState.IsCuffed(target.Id) || PoliceState.SuspectOf(habbo.Id) == target.Id);
    }
}
