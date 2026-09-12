using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: `:rot` - turn the piece you are facing.
///
/// Rotating by hand means picking a piece up into the drag state and dropping
/// it again, which re-levels it, risks losing the tile, and is impossible for
/// anything you are standing on. `:rot` turns it where it sits.
///
/// Bare `:rot` is a quarter turn, the same step the client's own rotate makes.
/// `:rot 4` names the facing outright, which is what makes a row of chairs line
/// up without eight separate clicks.
///
/// It turns BOTS as well, and that is not a bonus feature - it is the only way
/// to aim one. Nothing in the client can rotate a bot, so a bot faced wherever
/// it happened to stop walking. See UpdateBots() in RoomUserManager for the
/// other half of that fix: the facing now survives a restart.
/// </summary>
internal class RotateCommand : IChatCommand
{
    private readonly IDatabase _database;

    public RotateCommand(IDatabase database)
    {
        _database = database;
    }

    public string Key => "rot";
    public string PermissionRequired => "command_rot";

    public string Parameters => "%direction%";

    public string Description => "Turns the furniture or bot you are facing. :rot on its own is a quarter turn, or name a direction from 0 to 7.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var actor = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (actor == null)
            return;

        // The same gate as placing or dragging a piece. Saying it out loud
        // matters more for a typed command than a clicked one - there is no
        // greyed-out button to explain the silence.
        if (!room.CheckRights(session, false, true))
        {
            session.SendWhisper("You need rights in this room to build here.");
            return;
        }

        int? wanted = null;

        if (parameters.Any())
        {
            if (!int.TryParse(parameters[0], out var direction) || direction < 0 || direction > 7)
            {
                session.SendWhisper("Pick a direction from 0 to 7, or type :rot on its own for a quarter turn.");
                return;
            }

            wanted = direction;
        }

        BuildTarget.Find(room, actor, out var bot, out var item);

        if (bot != null)
        {
            // Avatars have four body facings, not eight. Rounding down to the
            // even one is what the client does with its own rotations, so an
            // odd number turns the bot rather than doing nothing.
            var facing = ((wanted ?? bot.RotBody + 2) % 8 + 8) % 8 / 2 * 2;

            bot.RotBody = facing;
            bot.RotHead = facing;
            bot.UpdateNeeded = true;

            // Written through immediately as well as into BotData: UpdateBots()
            // only runs when the room unloads, and a bot posed for a room that
            // never empties would otherwise be undone by the next restart.
            bot.BotData.Rot = facing;

            using (var dbClient = _database.GetQueryReactor())
            {
                dbClient.SetQuery("UPDATE `bots` SET `rotation` = @rotation WHERE `id` = @botId LIMIT 1");
                dbClient.AddParameter("rotation", facing);
                dbClient.AddParameter("botId", bot.BotData.Id);
                dbClient.RunQuery();
            }

            // A bot that still roams will turn again the moment it takes a
            // step, and the facing you just set is the one that gets saved -
            // so it is worth saying rather than letting it look broken.
            session.SendWhisper(bot.BotData.WalkingMode == "stand"
                ? $"{bot.BotData.Name} is now facing {facing}."
                : $"{bot.BotData.Name} is now facing {facing}, but will turn again as it walks. Set it to stand still to keep it.");
            return;
        }

        if (item == null)
        {
            session.SendWhisper("There is nothing there to turn. Face the piece, or stand on it.");
            return;
        }

        var rotation = wanted ?? (item.Rotation + 2) % 8;
        var name = string.IsNullOrEmpty(item.Definition.PublicName) ? item.Definition.ItemName : item.Definition.PublicName;

        // Nothing to do, and nothing to undo either - naming a facing a piece
        // already has is a no-op, and recording one would throw away the
        // snapshot of the last change the builder can still put back.
        if (rotation == item.Rotation)
        {
            session.SendWhisper($"{name} is already facing {rotation}.");
            return;
        }

        // Captured before the turn and only kept if the turn happens, the same
        // bargain MoveObjectEvent makes: :undo promises to put back the last
        // piece you ROTATED, and a rotation typed rather than dragged is still
        // a rotation. A refused turn changed nothing and must not spend the
        // snapshot the builder can still use.
        var before = FurniUndoState.Capture(item);

        // A single tile turns in place. Its footprint cannot change, so none of
        // SetFloorItem's placement checks apply - including the one that refuses
        // a tile with somebody on it, which would otherwise make it impossible
        // to turn the piece you are standing on.
        if (item.Definition.Length <= 1 && item.Definition.Width <= 1)
        {
            item.Rotation = rotation;
            item.UpdateState();
        }
        else
        {
            // Anything larger sweeps new tiles as it turns, so it goes through
            // the placement path and is allowed to be refused.
            var buildHeight = room.GetRoomUserManager().BuildHeightFor(session.GetHabbo().Id);

            if (!room.GetRoomItemHandler().SetFloorItem(session, item, item.GetX, item.GetY, rotation, false, false, true,
                    height: (buildHeight >= 0) ? buildHeight : item.CustomHeight))
            {
                session.SendWhisper($"{name} has no room to turn there. Move it, or step out of the way.");
                return;
            }
        }

        actor.LastFurniUndo = before;

        session.SendWhisper($"{name} turned to {rotation}.");
    }
}
