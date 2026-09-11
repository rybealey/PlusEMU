using System.Globalization;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: `:bh 2.5` - a sticky build height. Everything the builder places or
/// drags from then on sits at that height instead of stacking on whatever is
/// underneath it. `:bh` on its own puts it back to normal.
///
/// This is deliberately NOT the infostand Tools panel, which already sets one
/// item's height (UpdateMagicTileEvent). The point of a build height is not
/// having to touch each piece afterwards.
///
/// The height itself is applied in PlaceObjectEvent and MoveObjectEvent, which
/// pass it to SetFloorItem; this command only records it.
/// </summary>
internal class BuildHeightCommand : IChatCommand
{
    // Matches the stack-height ceiling the infostand widget and
    // RpFurniFunctionEvent already use, so the two tools cannot disagree about
    // what a legal height is.
    private const double MaximumHeight = 40;

    public string Key => "bh";
    public string PermissionRequired => "command_bh";

    public string Parameters => "%height%";

    public string Description => "Places furniture at a fixed height instead of stacking it. Type :bh on its own to turn it off.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null)
            return;

        // Bare `:bh`, or `:bh off`, goes back to normal stacking. Turning it off
        // needs no rights: someone who has lost rights mid-build must still be
        // able to clear a height they already set.
        if (!parameters.Any() || parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            if (user.BuildHeight == null)
            {
                session.SendWhisper("Build height is already off.");
                return;
            }

            user.BuildHeight = null;
            session.SendWhisper("Build height off. Furniture stacks normally again.");
            return;
        }

        // InvariantCulture, because the number came from a chat line rather than
        // the server's locale - `2.5` has to mean two and a half everywhere.
        if (!double.TryParse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            session.SendWhisper("That is not a height. Try :bh 2.5, or :bh on its own to turn it off.");
            return;
        }

        if (height < 0 || height > MaximumHeight)
        {
            session.SendWhisper($"Pick a height between 0 and {Format(MaximumHeight)}.");
            return;
        }

        // The same gate the stack tool uses. Placing furni needs rights anyway,
        // so this changes nothing a builder could otherwise do - it just says so
        // now instead of leaving them wondering why nothing moved.
        if (!room.CheckRights(session, false, true) &&
            !session.GetHabbo().Permissions.HasRight("room_item_use_any_stack_tile"))
        {
            session.SendWhisper("You need rights in this room to build here.");
            return;
        }

        user.BuildHeight = height;
        session.SendWhisper($"Build height set to {Format(height)}. Furniture you place or move will sit there until you type :bh.");
    }

    private static string Format(double height) => height.ToString("0.##", CultureInfo.InvariantCulture);
}
