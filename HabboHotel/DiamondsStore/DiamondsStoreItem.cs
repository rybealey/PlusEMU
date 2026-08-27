namespace Plus.HabboHotel.DiamondsStore;

public class DiamondsStoreItem
{
    public int Id { get; set; }
    public string ItemKey { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public int Price { get; set; }
    public int? SpecialPrice { get; set; }
    public int VipDays { get; set; }
    public int SortOrder { get; set; }

    public int EffectivePrice => SpecialPrice ?? Price;
}
