using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: `:ss 2` - a sticky place state (Twist, 2026-10-22). Every furni the
/// builder places from then on goes down in state 2 instead of the state it
/// had in the inventory; `:ss` on its own turns it off. The same shape as :bh,
/// which does it for height.
///
/// Any number, whatever the furni (Twist: ":ss can be any number depending on
/// the state") - not capped at its click count, because some furni draw states
/// past it (a number block's 100 faces, a state the server sets itself), and
/// the builder sees at once what a number looks like. What it is never written
/// into is extra data that is not a plain state number - a photo keeps its
/// image there, a group furni its group, a background its map, and a number
/// would blank them; those go down as they are. Only plain furni and gates take
/// one (TakesPlaceState).
///
/// The state itself is applied in PlaceObjectEvent; this command only records
/// it on the RoomUser, so it is forgotten when the builder leaves the room.
/// </summary>
internal class SetStateCommand : IChatCommand
{
    public string Key => "ss";
    public string PermissionRequired => "command_ss";

    public string Parameters => "%state%";

    public string Description => "Places furniture in the state you pick. :ss 2, then :ss on its own to turn it off.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null)
            return;

        // Bare `:ss`, or `:ss off`, turns it off. Like :bh, that needs no rights:
        // a builder who lost rights mid-build can still clear what they set.
        if (!parameters.Any() || parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            if (user.PlaceState == null)
            {
                session.SendWhisper("Place state is already off.");
                return;
            }

            user.PlaceState = null;
            session.SendWhisper("Place state off. Furniture goes down in its own state again.");
            return;
        }

        if (!int.TryParse(parameters[0], out var state) || state < 0)
        {
            session.SendWhisper("That is not a state. Try :ss 2, or :ss on its own to turn it off.");
            return;
        }

        if (!room.CheckRights(session, false, true))
        {
            session.SendWhisper("You need rights in this room to build here.");
            return;
        }

        user.PlaceState = state;
        session.SendWhisper($"Place state set to {state}. Furniture you place goes down in state {state} until you type :ss.");
    }

    /// <summary>
    /// Whether a piece about to be placed can be put in a place state: plain
    /// furni and gates, whose extra data is a state number.
    /// </summary>
    internal static bool TakesPlaceState(Item item) =>
        item.ExtraData is LegacyDataFormat
        && item.Definition.InteractionType is InteractionType.None or InteractionType.Gate;
}
