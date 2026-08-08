using System.Text.Json;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class PurchasePhotoEvent : IPacketEvent
{
    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IItemDataManager _itemDataManager;
    private readonly IItemFactory _itemFactory;

    public PurchasePhotoEvent(ICameraPhotoManager cameraPhotoManager, IItemDataManager itemDataManager, IItemFactory itemFactory)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _itemDataManager = itemDataManager;
        _itemFactory = itemFactory;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Protocol note: CameraPurchaseOK has no failure variant, so a missing
        // pending photo cannot send a "failed" reply — silent return + the
        // packet-exception logging is the agreed behavior (spec deviation, ok'd).
        //
        // Idempotency: TryConsumePurchase flips the pending photo's Purchased
        // flag at most once. A false return with `pending` still populated
        // means this photo was already purchased (double-click/retry) — the
        // client's UI is still waiting on a reply, so we resend the same OK
        // without creating a second inventory item. A false return with
        // `pending` null means no photo was ever rendered, preserving the
        // original silent-return behavior.
        if (!_cameraPhotoManager.TryConsumePurchase(session.GetHabbo().Id, out var pending))
        {
            if (pending == null)
                return Task.CompletedTask;

            session.Send(new CameraPurchaseOkComposer());
            return Task.CompletedTask;
        }

        if (!_itemDataManager.Items.TryGetValue(CameraPhotoItem.BaseItemId, out var definition))
            return Task.CompletedTask;

        var extradata = JsonSerializer.Serialize(new
        {
            t = pending.TakenUnixMs,
            u = pending.PhotoId,
            n = session.GetHabbo().Username,
            s = session.GetHabbo().Id,
            w = pending.Url,
        });

        var item = _itemFactory.CreateSingleItemNullable(definition, session.GetHabbo(), extradata, "");
        if (item != null)
        {
            if (session.GetHabbo().Inventory.Furniture.AddItem(item.ToInventoryItem()))
                session.Send(new FurniListNotificationComposer(item.Id, 1));
            session.Send(new FurniListUpdateComposer());
            session.Send(new CameraPurchaseOkComposer());
        }
        return Task.CompletedTask;
    }
}
