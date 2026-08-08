using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.Communication.Packets.Outgoing.Catalog;

public class CatalogPageComposer : IServerPacket
{
    private readonly CatalogPage _page;
    private readonly string _mode;
    public uint MessageId => ServerPacketHeader.CatalogPageComposer;

    public CatalogPageComposer(CatalogPage page, string mode)
    {
        _page = page;
        _mode = mode;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_page.Id);
        packet.WriteString(_mode);
        packet.WriteString(_page.Layout);
        packet.WriteInteger(_page.PageStringsList1.Count);
        foreach (var s in _page.PageStringsList1) packet.WriteString(s);
        packet.WriteInteger(_page.PageStringsList2.Count);
        foreach (var s in _page.PageStringsList2) packet.WriteString(s);
        if (!_page.Layout.Equals("frontpage") && !_page.Layout.Equals("club_buy"))
        {
            packet.WriteInteger(_page.Items.Count);
            foreach (var item in _page.Items.Values)
            {
                packet.WriteInteger(item.Id);
                packet.WriteString(item.CatalogName);
                packet.WriteBoolean(false); //IsRentable
                packet.WriteInteger(item.CostCredits);
                if (item.CostDiamonds > 0)
                {
                    packet.WriteInteger(item.CostDiamonds);
                    packet.WriteInteger(5); // Diamonds
                }
                else
                {
                    packet.WriteInteger(item.CostPixels);
                    packet.WriteInteger(0); // Type of PixelCost
                }
                packet.WriteBoolean(ItemUtility.CanGiftItem(item));
                if (item.Definition.InteractionType == InteractionType.Deal || item.Definition.InteractionType == InteractionType.Roomdeal)
                {
                    CatalogDeal deal = null;
                    if (!PlusEnvironment.Game.Catalog.TryGetDeal(item.Definition.BehaviourData, out deal))
                        packet.WriteInteger(0); //Count
                    else
                    {
                        packet.WriteInteger(deal.ItemDataList.Count);
                        foreach (var dealItem in deal.ItemDataList.ToList())
                        {
                            packet.WriteString(dealItem.Definition.Type.ToString());
                            packet.WriteInteger(dealItem.Definition.SpriteId);
                            packet.WriteString("");
                            packet.WriteInteger(dealItem.Amount);
                            packet.WriteBoolean(false);
                        }
                    }
                }
                else
                {
                    packet.WriteInteger(string.IsNullOrEmpty(item.Badge) ? 1 : 2); //Count 1 item if there is no badge, otherwise count as 2.
                    if (!string.IsNullOrEmpty(item.Badge))
                    {
                        packet.WriteString("b");
                        packet.WriteString(item.Badge);
                    }
                    // Bots ship as product type "r" (robot); the client renders the
                    // avatar from the figure sent in extraParam. Detect a bot via its
                    // catalog_bot_presets entry (keyed by item_id), not the furniture
                    // interaction_type - the seeded data has that as "default", so
                    // ToCharCode() would emit "i" and the client would draw a blank
                    // wall item instead of the bot.
                    var isBot = PlusEnvironment.Game.Catalog.TryGetBot(item.ItemId, out var catalogBot);
                    packet.WriteString(isBot ? "r" : item.Definition.Type.ToCharCode().ToLower());
                    if (item.Definition.Type.ToString().ToLower() == "b")
                    {
                        //This is just a badge, append the name.
                        packet.WriteString(item.Definition.ItemName);
                    }
                    else
                    {
                        packet.WriteInteger(item.Definition.SpriteId);
                        if (item.Definition.InteractionType == InteractionType.Wallpaper || item.Definition.InteractionType == InteractionType.Floor || item.Definition.InteractionType == InteractionType.Landscape)
                            packet.WriteString(item.CatalogName.Split('_')[2]);
                        else if (isBot) //Bots: figure from the preset -> client renders the avatar
                            packet.WriteString(catalogBot.Figure);
                        else if (item.ExtraData != null) packet.WriteString(item.ExtraData != null ? item.ExtraData : string.Empty);
                        packet.WriteInteger(item.Amount);
                        packet.WriteBoolean(item.IsLimited); // IsLimited
                        if (item.IsLimited)
                        {
                            packet.WriteUInteger(item.LimitedEditionStack);
                            packet.WriteUInteger(item.LimitedEditionStack - item.LimitedEditionSells);
                        }
                    }
                }
                packet.WriteInteger(0); //club_level
                packet.WriteBoolean(ItemUtility.CanSelectAmount(item));
                packet.WriteBoolean(false); // TODO: Figure out
                packet.WriteString(""); //previewImage -> e.g; catalogue/pet_lion.png
            }
        }
        else
            packet.WriteInteger(0);
        packet.WriteInteger(-1);
        packet.WriteBoolean(false);
        packet.WriteInteger(PlusEnvironment.Game.Catalog.Promotions.ToList().Count); //Count
        foreach (var promotion in PlusEnvironment.Game.Catalog.Promotions.ToList())
        {
            packet.WriteInteger(promotion.Id);
            packet.WriteString(promotion.Title);
            packet.WriteString(promotion.Image);
            packet.WriteInteger(promotion.Unknown);
            packet.WriteString(promotion.PageLink);
            packet.WriteInteger(promotion.ParentId);
        }
    }
}