using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :heal &lt;player&gt;.
///
/// NOT AN ALIAS, AND THAT IS THE POINT OF IT BEING ITS OWN CLASS. For a
/// clocked-in hospital employee it does exactly what ":offer x heal" does, and
/// today that is all it does. It is going to mean other things to other people
/// - the word is the obvious one to type and the hotel will want it to work
/// for more than one kind of person - and every one of those is a branch off
/// the check below rather than a change to :offer.
///
/// Compare :sell, which IS an alias and says so by subclassing OfferCommand.
/// This one shares the routine and keeps its own front door.
///
/// The syringe is not checked here. It does not need to be: the catalogue
/// entry for "heal" names the tool, and the offer path enforces it for
/// whichever command got there - so the rule lives in one place and this
/// command cannot drift from it.
/// </summary>
internal class HealCommand : ITargetChatCommand
{
    public string Key => "heal";

    public string PermissionRequired => "command_heal";

    public string Parameters => "%target%";

    public string Description => "Offer a heal to another player.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "Heal who? :heal <player>";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || target == null || room == null)
            return Task.CompletedTask;

        if (MedicalUtility.IsOnDutyHospitalStaff(habbo.Id))
            return OfferCommand.Place(session, room, target, "heal", 1);

        // EVERY OTHER CASE LANDS HERE, and there is deliberately only one of
        // them so far. The refusal is worded for healing rather than for
        // selling, because ":offer x heal" and ":heal x" are the same act to
        // the server and two different sentences to the person typing them.
        session.SendWhisper(MedicalUtility.IsHospitalStaff(habbo.Id)
            ? "You have to be on duty to treat somebody. Clock in from the Corporations drawer."
            : "Only hospital staff can treat somebody right now.");
        return Task.CompletedTask;
    }
}
