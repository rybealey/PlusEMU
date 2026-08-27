namespace Plus.HabboHotel.DiamondsStore;

public interface IDiamondsStoreManager
{
    Task Init();
    IReadOnlyList<DiamondsStoreItem> Items { get; }
    bool TryGetItem(string itemKey, out DiamondsStoreItem item);
}
