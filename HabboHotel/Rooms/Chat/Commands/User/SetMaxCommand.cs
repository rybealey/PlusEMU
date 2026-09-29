using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

internal class SetMaxCommand : IChatCommand
{
    private readonly IDatabase _database;
    public string Key => "setmax";
    public string PermissionRequired => "command_setmax";

    public string Parameters => "%value%";

    public string Description => "Set the visitor limit to the room.";

    public SetMaxCommand(IDatabase database)
    {
        _database = database;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // pixelrp: the visitor limit is a room setting, and room settings are
        // staff only (Room.CanManageSettings) - owners no longer set it
        if (!room.CanManageSettings(session))
            return;
        if (!parameters.Any())
        {
            session.SendWhisper("Please enter a value for the room visitor limit.");
            return;
        }
        if (int.TryParse(parameters[0], out var maxAmount))
        {
            if (maxAmount == 0)
            {
                maxAmount = 10;
                session.SendWhisper("visitor amount too low, visitor amount has been set to 10.");
            }
            else if (maxAmount > RoomLimits.MaxVisitors && !session.GetHabbo().Permissions.HasRight("override_command_setmax_limit"))
            {
                maxAmount = RoomLimits.MaxVisitors;
                session.SendWhisper($"visitor amount too high for your rank, visitor amount has been set to {RoomLimits.MaxVisitors}.");
            }
            else
                session.SendWhisper($"visitor amount set to {maxAmount}.");
            room.UsersMax = maxAmount;
            using var dbClient = _database.GetQueryReactor();
            dbClient.RunQuery($"UPDATE `rooms` SET `users_max` = {maxAmount} WHERE `id` = '{room.Id}' LIMIT 1");
        }
        else
            session.SendWhisper("Invalid amount, please enter a valid number.");
    }
}