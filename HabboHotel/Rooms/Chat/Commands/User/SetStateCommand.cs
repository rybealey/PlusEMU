using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: `:ss` - set the state of the piece you are facing.
///
/// A switchable piece only moves one state per click, so reaching state 7 of a
/// gate means clicking it seven times and counting. `:ss 7` goes there, which
/// is the difference between posing a room and operating it.
///
/// States are the same numbers the furni itself uses - 0 up to one less than
/// its mode count - and a piece with nothing to switch says so rather than
/// having a number written into its data. That guard matters: extra_data is
/// also where a photo keeps its image and a background keeps its map, and a
/// stray "3" would blank them.
/// </summary>
internal class SetStateCommand : IChatCommand
{
    public string Key => "ss";
    public string PermissionRequired => "command_ss";

    public string Parameters => "%state%";

    public string Description => "Sets the state of the furniture you are facing. :ss 3, or :ss on its own for the next one.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var actor = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (actor == null)
            return;

        if (!room.CheckRights(session, false, true))
        {
            session.SendWhisper("You need rights in this room to build here.");
            return;
        }

        if (!BuildTarget.FindFurni(room, actor, out var item))
        {
            session.SendWhisper("There is nothing there to set. Face the piece, or stand on it.");
            return;
        }

        var name = string.IsNullOrEmpty(item.Definition.PublicName) ? item.Definition.ItemName : item.Definition.PublicName;

        // The same ceiling the click interactor uses: Modes is the count, so the
        // highest legal state is one below it.
        var highest = item.Definition.Modes - 1;

        if (highest <= 0)
        {
            session.SendWhisper($"{name} has only one state.");
            return;
        }

        int state;

        if (parameters.Any())
        {
            if (!int.TryParse(parameters[0], out state) || state < 0 || state > highest)
            {
                session.SendWhisper($"{name} goes from 0 to {highest}.");
                return;
            }
        }
        else
        {
            // Bare `:ss` is one click, wrap included - so it can be held down
            // through a cycle without doing the arithmetic.
            if (!int.TryParse(item.LegacyDataString, out var current))
                current = 0;

            state = (current >= highest) ? 0 : current + 1;
        }

        item.LegacyDataString = state.ToString();

        // The setter is a no-op on anything whose extra data is not the legacy
        // string format - a photo, a background, a crackable. Modes should have
        // ruled those out already, so this is a belt on top of braces: better a
        // sentence than a command that reports a state the piece never took.
        if (item.LegacyDataString != state.ToString())
        {
            session.SendWhisper($"{name} does not take a numbered state.");
            return;
        }

        item.UpdateState();

        session.SendWhisper($"{name} set to state {state}.");
    }
}
