using Dapper;
using Plus.Database;
using Plus.HabboHotel.Users.UserData;

namespace Plus.HabboHotel.Users.Ignores;

internal class IgnoresUserDataLoadingTask : IUserDataLoadingTask
{
    private readonly IDatabase _database;

    public IgnoresUserDataLoadingTask(IDatabase database)
    {
        _database = database;
    }

    public async Task Load(Habbo habbo)
    {
        using var connection = _database.Connection();
        // pixelrp: the ignored ids, by name. `SELECT *` mapped its first column,
        // and since migration 4 dropped `id` that is `user_id` - every player's
        // list was their own id, so nobody they ignored stayed ignored past a log in.
        var ignoredUsers = (await connection.QueryAsync<int>("SELECT `ignore_id` FROM `user_ignores` WHERE `user_id` = @userId", new { userId = habbo.Id })).ToList();
        habbo.IgnoresComponent = new(ignoredUsers);
    }
}