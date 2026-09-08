using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :setstock &lt;acronym&gt; &lt;quantity&gt; - set what a corporation is
/// holding.
///
/// Staff only. Nothing in the game writes rp_corporations.stock yet - farming
/// and mining are what will - so this is how the number moves at all, and how
/// the phone's Stocks app can be seen doing anything before then.
///
/// A reading is stamped the moment the value changes rather than waiting for
/// the ledger's next tick, so the app and its chart move immediately.
/// </summary>
internal class SetStockCommand : IChatCommand
{
    public string Key => "setstock";
    public string PermissionRequired => "command_setstock";

    public string Parameters => "%acronym% %quantity%";

    public string Description => "Set a corporation's stock level.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 2)
        {
            session.SendWhisper("Use :setstock <acronym> <quantity> - for example :setstock EA 240.");
            return;
        }

        if (!int.TryParse(parameters[1], out var quantity) || quantity < 0)
        {
            session.SendWhisper($"'{parameters[1]}' is not a quantity. Whole numbers, zero or more.");
            return;
        }

        var acronym = parameters[0].ToUpperInvariant();

        using var connection = PlusEnvironment.DatabaseManager.Connection();

        var corp = connection.QuerySingleOrDefault<(int Id, string Name, int Capacity)>(
            "SELECT `id`, `name`, `stock_capacity` AS Capacity FROM `rp_corporations` " +
            "WHERE UPPER(`acronym`) = @acronym AND `acronym` != '' LIMIT 1", new { acronym });

        if (corp.Id == 0)
        {
            session.SendWhisper($"No corporation with the acronym '{parameters[0]}'.");
            return;
        }

        connection.Execute("UPDATE `rp_corporations` SET `stock` = @quantity WHERE `id` = @id",
            new { quantity, id = corp.Id });

        // So the app moves now rather than on the next tick.
        StockLedger.Record(corp.Id, quantity);

        // Capacity is what makes a number mean anything to a farmer, so say
        // where this leaves them rather than only echoing the number back.
        var reading = (corp.Capacity > 0)
            ? $"{quantity} of {corp.Capacity} ({(int)Math.Round(quantity * 100.0 / corp.Capacity)}% full)"
            : $"{quantity}, with no capacity set";

        session.SendWhisper($"{corp.Name} is now holding {reading}.");
    }
}
