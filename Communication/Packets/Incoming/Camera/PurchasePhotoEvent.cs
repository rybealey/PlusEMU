using System.Text.Json;
using Dapper;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class PurchasePhotoEvent : IPacketEvent
{
    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IItemDataManager _itemDataManager;
    private readonly IItemFactory _itemFactory;
    private readonly IDatabase _database;

    public PurchasePhotoEvent(ICameraPhotoManager cameraPhotoManager, IItemDataManager itemDataManager, IItemFactory itemFactory, IDatabase database)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _itemDataManager = itemDataManager;
        _itemFactory = itemFactory;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
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
                return;

            session.Send(new CameraPurchaseOkComposer());
            return;
        }

        // From here the photo is reserved as purchased. If we cannot actually
        // create the item, roll the reservation back so a retry re-attempts
        // rather than getting a false "already purchased" OK with no item.
        if (!_itemDataManager.Items.TryGetValue(CameraPhotoItem.BaseItemId, out var definition))
        {
            _cameraPhotoManager.ResetPurchase(session.GetHabbo().Id);
            return;
        }

        var extradata = JsonSerializer.Serialize(new
        {
            // seconds, not millis: the client renders this as
            // `new Date(t * 1000)` (CameraWidgetShowPhotoView), so a millis
            // value here lands ~58000 years in the future.
            t = pending.TakenUnixMs / 1000,
            u = pending.PhotoId,
            n = session.GetHabbo().Username,
            s = session.GetHabbo().Id,
            w = pending.Url,
        });

        var item = _itemFactory.CreateSingleItemNullable(definition, session.GetHabbo(), extradata, "");
        if (item == null)
        {
            _cameraPhotoManager.ResetPurchase(session.GetHabbo().Id);
            return;
        }

        if (session.GetHabbo().Inventory.Furniture.AddItem(item.ToInventoryItem()))
            session.Send(new FurniListNotificationComposer(item.Id, 1));
        session.Send(new FurniListUpdateComposer());
        session.Send(new CameraPurchaseOkComposer());

        // pixelrp: purchased photos also land in the player's private photo
        // library (the phone's Photos app) as a hidden camera_web row —
        // PublishPhoto flips `visible` to put the same row on the CMS page.
        using var connection = _database.Connection();
        await connection.ExecuteAsync(
            "INSERT INTO `camera_web` (`user_id`, `room_id`, `timestamp`, `url`, `visible`) VALUES (@userId, @roomId, @timestamp, @url, 0)",
            new { userId = session.GetHabbo().Id, roomId = pending.RoomId, timestamp = pending.TakenUnixMs / 1000, url = pending.Url });
    }
}
