using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;

namespace Plus.HabboHotel.DiamondsStore;

// pixelrp: in-game Store tab listings (VIP tokens and future diamond items).
// special_price, when non-NULL, overrides price and renders as a sale.
public class DiamondsStoreManager : IDiamondsStoreManager
{
    private readonly IDatabase _database;
    private readonly ILogger<DiamondsStoreManager> _logger;
    private List<DiamondsStoreItem> _items = new();

    public DiamondsStoreManager(IDatabase database, ILogger<DiamondsStoreManager> logger)
    {
        _database = database;
        _logger = logger;
    }

    public IReadOnlyList<DiamondsStoreItem> Items => _items;

    public async Task Init()
    {
        using var connection = _database.Connection();
        _items = (await connection.QueryAsync<DiamondsStoreItem>(
            "SELECT `id`, `item_key` AS ItemKey, `name`, `description`, `icon`, `price`, `special_price` AS SpecialPrice, `vip_days` AS VipDays, `sort_order` AS SortOrder " +
            "FROM `diamonds_store_items` WHERE `enabled` = 1 ORDER BY `sort_order`")).ToList();
        _logger.LogInformation("Loaded " + _items.Count + " diamonds store items.");
    }

    public bool TryGetItem(string itemKey, out DiamondsStoreItem item)
    {
        item = _items.FirstOrDefault(candidate => candidate.ItemKey == itemKey);
        return item != null;
    }
}
