using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Apartments;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Navigator;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.Trading;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: Apartments (Twist, 2026-10-28; the "Apartments" canvas, B + C).
/// Any player can buy one apartment - a room of their own - for
/// <see cref="Price"/> cash in hand, at an Apartments point (furni with the
/// `apartments` behaviour, SF Apartments' action point, item 11561): step on
/// it and the window opens, step off and it closes, as the Marketplace point
/// does.
///
/// An apartment is drawn on a <see cref="Grid"/> x <see cref="Grid"/> grid of
/// lots, each <see cref="LotSize"/> tiles square. The first one bought is the
/// centre lot; more lots that SHARE A SIDE with it can be bought, each one
/// <see cref="LotPriceStep"/> dearer than the last (<see cref="LotPrice"/>),
/// and the room grows by that lot. One on every lot is a Penthouse
/// (<see cref="IsPenthouse"/>). `rp_apartments.lots` keeps the lots it
/// covers. Every lot has the same tiles in the room whichever others it has
/// (<see cref="FloorPlan"/>), so nothing placed ever moves when one is added.
///
/// The room is made whole in ONE transaction: the cash, the room, its floor
/// plan, the arrow on its door and the ownership row all happen, or none do.
/// The arrow (`apartment_exit`, see <see cref="LeaveSoon"/>) leads back to the
/// room it was bought in - players have no navigator.
///
/// Selling back (Twist) returns <see cref="SellBackPercent"/>% of everything
/// paid, and the furni inside goes back to its owners' inventories; the room
/// is deleted. Listing for sale puts it on the real estate board at an asking
/// price for a fee of <see cref="ListingFee"/> (the board itself is still to be
/// built); a buyer there gets the furni inside with it. There is no rent (Twist).
///
/// The window's packet (RpApartmentActionEvent) is answered only for somebody
/// standing on an Apartments point, so nothing can be bought, sold or entered
/// from anywhere else. Every change runs under one lock.
/// </summary>
public static class RpApartments
{
    public const int Refresh = 0;
    public const int Buy = 1;
    public const int Enter = 2;
    public const int SellBack = 3;
    public const int List = 4;
    public const int Unlist = 5;

    /// <summary>What an apartment - its first lot, the centre one - costs, cash in hand (Twist, 2026-10-28).</summary>
    public const int Price = 5000;

    /// <summary>
    /// What the second lot costs; each one after costs <see cref="LotPriceStep"/>
    /// more (Twist). Halved from $5,000 + $1,000 a lot (Twist, 2026-10-28: a
    /// full penthouse at $401,000 was too dear) - every lot now comes to $203,000.
    /// </summary>
    public const int LotPriceBase = 2500;

    public const int LotPriceStep = 500;

    /// <summary>How much of everything paid selling back returns (Twist).</summary>
    public const int SellBackPercent = 50;

    /// <summary>The most an apartment may be listed for on the real estate board.</summary>
    public const int MaxListPrice = 1_000_000;

    /// <summary>What putting an apartment on the real estate board costs, cash in hand, each time (Twist, 2026-10-28).</summary>
    public const int ListingFee = 100;

    /// <summary>Lots along each side of the grid an apartment is drawn on.</summary>
    public const int Grid = 5;

    /// <summary>Tiles along each side of one lot.</summary>
    public const int LotSize = 8;

    /// <summary>The middle lot - the first one every apartment has.</summary>
    public const int CentreLot = (Grid * Grid) / 2;

    /// <summary>The arrow put on an apartment's door, by its furni name.</summary>
    private const string ExitFurni = "tp_arrow";

    /// <summary>The least time between two changes (buy, go, sell, list) from one player.</summary>
    private const long ActionGapMs = 300;

    /// <summary>Every change to an apartment - here and on the real estate board (RpRealEstate) - runs under this one lock.</summary>
    internal static readonly object Lock = new();

    /// <summary>Whose window an Apartments point opened. Only they are answered.</summary>
    private static readonly ConcurrentDictionary<int, byte> AtPoint = new();

    private static readonly ConcurrentDictionary<int, long> LastAction = new();

    /// <summary>
    /// An owner walking out of their apartment, whose client was just asked
    /// for a picture of it (LeaveSoon) - the room, and when. The picture is
    /// stored for that room when it arrives, even once they are out of it
    /// (<see cref="TakeSnapshotRoom"/>).
    /// </summary>
    private static readonly ConcurrentDictionary<int, (uint RoomId, long At)> Snapshots = new();

    /// <summary>How long after the ask an apartment's picture is still taken.</summary>
    private const long SnapshotMs = 15_000;

    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.HabboHotel.Rooms.RpApartments");

    /// <summary>"$5,000".</summary>
    public static string Money(int value) => "$" + value.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>The price of an apartment's <paramref name="n"/>th lot: the apartment itself, then $2,500, $3,000, $3,500...</summary>
    public static int LotPrice(int n) => (n <= 1) ? Price : LotPriceBase + (n - 2) * LotPriceStep;

    public sealed class ApartmentRow
    {
        public uint RoomId { get; set; }
        public string Lots { get; set; } = string.Empty;
        public uint LobbyRoomId { get; set; }
        public int Paid { get; set; }
        public int BoughtAt { get; set; }
        public int ListPrice { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
    }

    /// <summary>Whether the player is standing on an Apartments point.</summary>
    public static bool IsAtPoint(Room? room, RoomUser? user)
    {
        if (room == null || user == null)
            return false;
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        return items != null && items.Any(item => item?.Definition?.InteractionType == InteractionType.Apartments);
    }

    /// <summary>
    /// The step onto an Apartments point. The step fires on the movement
    /// thread and the window reads the database, so - as RpMarket.OpenSoon - it
    /// runs on RoomTickWriter's thread, in order with <see cref="CloseSoon"/>,
    /// and only for somebody still standing on one by then.
    /// </summary>
    public static void OpenSoon(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        RoomTickWriter.QueueOrdered(() =>
        {
            // Same session as the step - a relog in between is a new one.
            if (client!.GetHabbo() != habbo)
                return;
            var room = habbo.CurrentRoom;
            if (!IsAtPoint(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
                return;
            AtPoint[habbo.Id] = 0;
            Send(client, true);
        });
    }

    /// <summary>
    /// Off an Apartments point - the step off, the click to walk (OwnWalk) or
    /// the room change: the window comes down and stops being answered. A
    /// close needs no database, so it is sent at once.
    /// </summary>
    public static void Close(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        if (!AtPoint.TryRemove(habbo.Id, out _))
            return;
        client!.Send(new InClientLinkComposer("apartments/hide"));
    }

    /// <summary><see cref="Close"/> for the step off, behind the open it follows.</summary>
    public static void CloseSoon(GameClient? client)
    {
        if (client?.GetHabbo() == null)
            return;
        RoomTickWriter.QueueOrdered(() => Close(client));
    }

    /// <summary>Logout: the window is shut for them, and their timer goes.</summary>
    public static void Forget(int userId)
    {
        AtPoint.TryRemove(userId, out _);
        LastAction.TryRemove(userId, out _);
        Snapshots.TryRemove(userId, out _);
    }

    /// <summary>
    /// The apartment a room picture from this player is for, when their client
    /// was just asked for one as they walked out of it - taken once
    /// (RenderRoomThumbnailEvent stores it for that room).
    /// </summary>
    public static bool TakeSnapshotRoom(int userId, out uint roomId)
    {
        roomId = 0;
        if (!Snapshots.TryRemove(userId, out var asked) || Environment.TickCount64 - asked.At > SnapshotMs)
            return false;
        roomId = asked.RoomId;
        return true;
    }

    /// <summary>
    /// One action from the window (RpApartmentActionEvent): buy (the lot
    /// picked), go, sell back, list (the asking price) or unlist. Answers with
    /// the window again.
    /// </summary>
    public static void Handle(GameClient session, int action, int value)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || action < Refresh || action > Unlist)
            return;

        // Only for somebody an Apartments point opened it for, and who is
        // still on it; the window is never up anywhere else.
        var room = habbo.CurrentRoom;
        if (!AtPoint.ContainsKey(habbo.Id) || !IsAtPoint(room, room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)))
        {
            Log.Warn("Apartments action {0} from {1} (id {2}) off an Apartments point.", action, habbo.Username, habbo.Id);
            Close(session);
            return;
        }

        if (action == Refresh)
        {
            Send(session!, false);
            return;
        }
        var now = Environment.TickCount64;
        if (LastAction.TryGetValue(habbo.Id, out var acted) && now - acted < ActionGapMs)
        {
            Send(session!, false, "Slow down - try again in a moment.");
            return;
        }
        LastAction[habbo.Id] = now;

        string? notice;
        try
        {
            notice = action switch
            {
                Buy => BuyLot(session!, room!, value),
                Enter => GoToApartment(session!, room!),
                SellBack => SellApartmentBack(session!),
                List => ListForSale(session!, value),
                Unlist => TakeOffSale(session!),
                _ => string.Empty
            };
        }
        catch (Exception e)
        {
            // Each change is one transaction, so a surprise here has bought or
            // sold nothing; this only stops the window going without an answer.
            ExceptionLogger.LogException(e);
            notice = "Something went wrong - try again.";
        }
        // Gone to their apartment: the window came down with the step off the point.
        if (notice != null)
            Send(session!, false, notice);
    }

    /// <summary>
    /// The window as this player sees it: the prices, the building, their
    /// apartment if they have one, and what to say about their last action.
    /// `open` brings the window up; without it a closed window ignores this.
    /// </summary>
    public static void Send(GameClient session, bool open, string notice = "")
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        var apartment = ApartmentOf(habbo.Id);
        session!.Send(new RpApartmentComposer(open, BuildingName(habbo.CurrentRoom?.Name), apartment, LotsOf(apartment), notice));
    }

    /// <summary>"SF Apartments [Interior]" -> "SF Apartments": the building, without the room's [tag].</summary>
    private static string BuildingName(string? roomName) =>
        Regex.Replace(roomName ?? string.Empty, @"\s*\[[^\]]*\]", string.Empty).Trim();

    /// <summary>Why this player cannot buy, sell or go to an apartment right now, or null.</summary>
    internal static string? Refusal(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (KnockedOut.Is(session))
            return "You are knocked out.";
        if (PoliceState.IsCuffed(habbo.Id))
            return "Your hands are cuffed.";
        if (JailState.IsJailed(habbo.Id))
            return "You cannot do that in jail.";
        if (RpTrades.IsTrading(habbo.Id))
            return RpTrades.FrozenMessage;
        return null;
    }

    /// <summary>Buy: the apartment itself (the centre lot) for somebody without one, otherwise one more lot.</summary>
    private static string BuyLot(GameClient session, Room lobby, int lot)
    {
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;
        if (lot < 0 || lot >= Grid * Grid)
            return "Pick a lot first.";
        var apartment = ApartmentOf(session.GetHabbo().Id);
        return (apartment == null) ? BuyApartment(session, lobby, lot) : AddLot(session, apartment, lot);
    }

    /// <summary>
    /// Buy an apartment: the centre lot, for <see cref="Price"/> cash. One
    /// transaction takes the cash (only if they still hold it), writes the
    /// ownership row (one per player - the key refuses a second), makes the
    /// room, its floor plan and the arrow on its door. Any failure undoes it all.
    /// </summary>
    private static string BuyApartment(GameClient session, Room lobby, int lot)
    {
        var habbo = session.GetHabbo();
        if (lot != CentreLot)
            return "Pick the centre lot to buy your apartment.";
        if (habbo.Credits < Price)
            return $"You need {Money(Price)} cash in hand to buy an apartment.";

        lock (Lock)
        {
            if (ApartmentOf(habbo.Id) != null)
                return "You already have an apartment.";

            var lots = new List<int> { CentreLot };
            var plan = FloorPlan(lots);
            var name = $"{habbo.Username}'s Apartment";
            if (name.Length > 60)
                name = name[..60];

            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var exitBase = connection.ExecuteScalar<int?>(
                "SELECT `id` FROM `furniture` WHERE `item_name` = @name LIMIT 1", new { name = ExitFurni }, transaction) ?? 0;
            if (exitBase == 0)
            {
                transaction.Rollback();
                Log.Error("Apartments: no `{0}` furni for the door arrow - nothing was bought.", ExitFurni);
                return "Apartments are not ready yet - please tell a member of staff.";
            }

            // The cash, only while they still hold it (Habbo.ChangeCredits keeps
            // users.credits current, so the database is the real test).
            if (!TakeCash(connection, transaction, habbo.Id, Price))
            {
                transaction.Rollback();
                return $"You need {Money(Price)} cash in hand to buy an apartment.";
            }

            var roomId = (uint)connection.ExecuteScalar<long>(
                "INSERT INTO `rooms` (`roomtype`, `caption`, `description`, `owner`, `model_name`, `category`, `users_max`, `trade_settings`, `state`) " +
                "VALUES ('private', @caption, @description, @me, 'model_a', @category, @usersMax, 2, 'locked'); SELECT LAST_INSERT_ID();",
                new
                {
                    caption = name,
                    description = $"{habbo.Username}'s home in {BuildingName(lobby.Name)}.",
                    me = habbo.Id,
                    category = RoomCategories.FallbackId,
                    usersMax = RoomLimits.DefaultVisitors
                }, transaction);

            // The floor plan editor's own model name, so an edit there changes
            // this model rather than starting another.
            var model = $"model_bc_{roomId}";
            connection.Execute(
                "INSERT INTO `room_models` (`id`, `door_x`, `door_y`, `door_z`, `door_dir`, `heightmap`, `public_items`, `custom`, `wall_height`) " +
                "VALUES (@model, @doorX, @doorY, 0, 2, @map, '', '1', -1)",
                new { model, doorX = plan.DoorX, doorY = plan.DoorY, map = plan.Map }, transaction);
            connection.Execute("UPDATE `rooms` SET `model_name` = @model WHERE `id` = @room LIMIT 1", new { model, room = roomId }, transaction);

            // The way out: an arrow on the door, facing into the room like the
            // door does, with the `apartment_exit` behaviour on it alone. The
            // PixelRP account's, not the player's, so :pickall leaves it be.
            var exitOwner = (PixelRpRooms.AccountId > 0) ? PixelRpRooms.AccountId : habbo.Id;
            var exitId = (uint)connection.ExecuteScalar<long>(
                "INSERT INTO `items` (`user_id`, `room_id`, `base_item`, `extra_data`, `x`, `y`, `z`, `rot`) " +
                "VALUES (@owner, @room, @baseItem, '', @x, @y, 0, 2); SELECT LAST_INSERT_ID();",
                new { owner = exitOwner, room = roomId, baseItem = exitBase, x = plan.DoorX, y = plan.DoorY }, transaction);
            connection.Execute(
                "INSERT INTO `rp_item_function` (`item_id`, `field`, `value`, `set_by`, `created_at`) " +
                "VALUES (@item, 'interaction_type', 'apartment_exit', 0, UNIX_TIMESTAMP())",
                new { item = exitId }, transaction);

            connection.Execute(
                "INSERT INTO `rp_apartments` (`user_id`, `room_id`, `lots`, `lobby_room_id`, `paid`, `bought_at`) " +
                "VALUES (@me, @room, @lots, @lobby, @price, UNIX_TIMESTAMP())",
                new { me = habbo.Id, room = roomId, lots = string.Join(",", lots), lobby = lobby.RoomId, price = Price }, transaction);

            transaction.Commit();

            habbo.ChangeCreditsInMemory(-Price);
            CoinLedger.Record(habbo, -Price, $"Apartments: bought an apartment (room {roomId})");
            session.Send(new CreditBalanceComposer(habbo.Credits));
            Log.Info("Apartments: {0} (id {1}) bought room {2} for {3}.", habbo.Username, habbo.Id, roomId, Price);
        }
        return "You bought an apartment! Press Go to my apartment to see it.";
    }

    /// <summary>
    /// One more lot, sharing a side with one they own, for <see cref="LotPrice"/>
    /// of its number. The room grows: its floor plan is drawn again from the
    /// lots, and its arrow follows the door should the door move. A room that
    /// is loaded is unloaded first - so the furni in it is saved and the new
    /// floor read - and everybody in it comes straight back in, as the floor
    /// plan editor does it.
    /// </summary>
    private static string AddLot(GameClient session, ApartmentRow apartment, int lot)
    {
        var habbo = session.GetHabbo();
        var lots = LotsOf(apartment);
        if (lots.Contains(lot))
            return "That lot is already yours.";
        if (!IsNextTo(lots, lot))
            return "Pick a lot next to your apartment.";
        var price = LotPrice(lots.Count + 1);
        if (habbo.Credits < price)
            return $"You need {Money(price)} cash in hand for that lot.";

        lock (Lock)
        {
            // Read again under the lock: the lots it is checked against are the ones it changes.
            var current = ApartmentOf(habbo.Id);
            if (current == null || current.RoomId != apartment.RoomId || current.Lots != apartment.Lots)
                return "Your apartment changed - try again.";

            var newLots = new List<int>(lots) { lot };
            var plan = FloorPlan(newLots);
            var model = string.IsNullOrEmpty(current.ModelName) ? $"model_bc_{current.RoomId}" : current.ModelName;

            var roomManager = PlusEnvironment.Game.RoomManager;
            var inside = new List<RoomUser>();
            if (roomManager.TryGetRoom(current.RoomId, out var loaded))
            {
                inside = loaded.GetRoomUserManager().GetRoomUsers().ToList();
                roomManager.UnloadRoom(current.RoomId);
            }

            var bought = false;
            try
            {
                using var connection = PlusEnvironment.DatabaseManager.Connection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                if (!TakeCash(connection, transaction, habbo.Id, price))
                {
                    transaction.Rollback();
                    return $"You need {Money(price)} cash in hand for that lot.";
                }
                var changed = connection.Execute(
                    "UPDATE `rp_apartments` SET `lots` = @lots, `paid` = `paid` + @price WHERE `user_id` = @me AND `lots` = @was LIMIT 1",
                    new { lots = string.Join(",", newLots), price, me = habbo.Id, was = current.Lots }, transaction);
                if (changed != 1)
                {
                    transaction.Rollback();
                    return "Your apartment changed - try again.";
                }
                connection.Execute(
                    "UPDATE `room_models` SET `heightmap` = @map, `door_x` = @doorX, `door_y` = @doorY, `door_z` = 0, `door_dir` = 2 WHERE `id` = @model LIMIT 1",
                    new { map = plan.Map, doorX = plan.DoorX, doorY = plan.DoorY, model }, transaction);
                // The arrow stays on the door wherever the door goes.
                connection.Execute(
                    "UPDATE `items` i JOIN `rp_item_function` f ON f.`item_id` = i.`id` AND f.`field` = 'interaction_type' AND f.`value` = 'apartment_exit' " +
                    "SET i.`x` = @x, i.`y` = @y, i.`z` = 0 WHERE i.`room_id` = @room",
                    new { x = plan.DoorX, y = plan.DoorY, room = current.RoomId }, transaction);
                // Every lot: "twist's Apartment" becomes "twist's Penthouse" (Twist).
                if (IsPenthouse(newLots))
                    connection.Execute(
                        "UPDATE `rooms` SET `caption` = CONCAT(LEFT(`caption`, CHAR_LENGTH(`caption`) - 9), 'Penthouse') WHERE `id` = @room AND `caption` LIKE '%Apartment' LIMIT 1",
                        new { room = current.RoomId }, transaction);
                transaction.Commit();
                bought = true;
            }
            finally
            {
                roomManager.ReloadModel(model);
                // Back into the room - on the new floor if it was bought.
                foreach (var user in inside)
                {
                    var client = user?.GetClient();
                    if (client?.GetHabbo() == null)
                        continue;
                    client.GetHabbo().CurrentRoom = null;
                    client.SendRoomForward(current.RoomId);
                }
            }

            if (bought)
            {
                habbo.ChangeCreditsInMemory(-price);
                CoinLedger.Record(habbo, -price, $"Apartments: bought lot {lot} (room {current.RoomId})");
                session.Send(new CreditBalanceComposer(habbo.Credits));
                Log.Info("Apartments: {0} (id {1}) bought lot {2} of room {3} for {4}.", habbo.Username, habbo.Id, lot, current.RoomId, price);
            }
        }
        return (LotsOf(ApartmentOf(habbo.Id)).Count == Grid * Grid)
            ? $"You bought the last lot for {Money(price)}. Your apartment is now a penthouse!"
            : $"You bought a lot for {Money(price)}. Your apartment is bigger now.";
    }

    /// <summary>
    /// Sell the apartment back: <see cref="SellBackPercent"/>% of everything
    /// paid for it, into their hand. The furni inside goes back to its owners'
    /// inventories (as a room delete does it); the arrow goes; anybody inside
    /// is sent to the room it was bought in - never left in a deleted room.
    /// Then one transaction deletes it and pays, only if the row is still
    /// theirs as read.
    /// </summary>
    private static string SellApartmentBack(GameClient session)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;

        lock (Lock)
        {
            var apartment = ApartmentOf(habbo.Id);
            if (apartment == null)
                return "You don't have an apartment.";
            var refund = (int)((long)apartment.Paid * SellBackPercent / 100);
            var roomManager = PlusEnvironment.Game.RoomManager;
            var lobby = (apartment.LobbyRoomId != 0 && apartment.LobbyRoomId != apartment.RoomId)
                ? apartment.LobbyRoomId
                : habbo.CurrentRoom?.RoomId ?? 0;

            // The furni first, while the room is loaded to hand it out.
            if (roomManager.TryLoadRoom(apartment.RoomId, out var room) && room != null)
            {
                var inside = room.GetRoomUserManager().GetRoomUsers().ToList();
                ReturnFurni(room);
                roomManager.UnloadRoom(apartment.RoomId);
                foreach (var user in inside)
                {
                    var client = user?.GetClient();
                    if (client?.GetHabbo() == null)
                        continue;
                    client.GetHabbo().CurrentRoom = null;
                    client.SendRoomForward(lobby);
                }
            }

            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Open();
            // The furni goes home first, on its own: taking it out of a loaded
            // room changes nothing in the database (RoomItemHandling.RemoveFurniture),
            // and should the sale below fail, the apartment is left empty
            // rather than its furni in two places. Everything but the arrow.
            connection.Execute(
                "UPDATE `items` i LEFT JOIN `rp_item_function` f ON f.`item_id` = i.`id` AND f.`field` = 'interaction_type' AND f.`value` = 'apartment_exit' " +
                "SET i.`room_id` = 0 WHERE i.`room_id` = @room AND f.`item_id` IS NULL",
                new { room = apartment.RoomId });

            using var transaction = connection.BeginTransaction();
            var removed = connection.Execute(
                "DELETE FROM `rp_apartments` WHERE `user_id` = @me AND `room_id` = @room LIMIT 1",
                new { me = habbo.Id, room = apartment.RoomId }, transaction);
            if (removed != 1)
            {
                transaction.Rollback();
                return "Your apartment changed - try again.";
            }
            connection.Execute("UPDATE `users` SET `credits` = `credits` + @refund WHERE `id` = @me LIMIT 1", new { refund, me = habbo.Id }, transaction);
            // The arrow and its behaviour go.
            connection.Execute(
                "DELETE i, f FROM `items` i JOIN `rp_item_function` f ON f.`item_id` = i.`id` AND f.`field` = 'interaction_type' AND f.`value` = 'apartment_exit' " +
                "WHERE i.`room_id` = @room",
                new { room = apartment.RoomId }, transaction);
            connection.Execute("DELETE FROM `rooms` WHERE `id` = @room LIMIT 1", new { room = apartment.RoomId }, transaction);
            connection.Execute("DELETE FROM `room_models` WHERE `id` = @model AND `custom` = '1' LIMIT 1", new { model = apartment.ModelName }, transaction);
            connection.Execute("DELETE FROM `room_rights` WHERE `room_id` = @room", new { room = apartment.RoomId }, transaction);
            connection.Execute("DELETE FROM `user_favorites` WHERE `room_id` = @room", new { room = apartment.RoomId }, transaction);
            connection.Execute("DELETE FROM `user_roomvisits` WHERE `room_id` = @room", new { room = apartment.RoomId }, transaction);
            connection.Execute("UPDATE `users` SET `home_room` = 0 WHERE `home_room` = @room", new { room = apartment.RoomId }, transaction);
            transaction.Commit();

            habbo.ChangeCreditsInMemory(refund);
            CoinLedger.Record(habbo, refund, $"Apartments: sold back (room {apartment.RoomId})");
            session.Send(new CreditBalanceComposer(habbo.Credits));
            Log.Info("Apartments: {0} (id {1}) sold room {2} back for {3}.", habbo.Username, habbo.Id, apartment.RoomId, refund);
            return $"You sold your apartment back for {Money(refund)}. Your furni is in your inventory.";
        }
    }

    /// <summary>
    /// Everything in the room taken out and, for owners online, put in their
    /// inventory at once, as DeleteRoomEvent does it; SellApartmentBack then
    /// moves every row to the inventory. The arrow is the exception: it is
    /// taken out, and the sale's transaction deletes it.
    /// </summary>
    private static void ReturnFurni(Room room)
    {
        var clients = PlusEnvironment.Game.ClientManager;
        foreach (var item in room.GetRoomItemHandler().GetWallAndFloor.ToList())
        {
            if (item == null)
                continue;
            if (item.Definition.InteractionType == InteractionType.ApartmentExit)
            {
                room.GetRoomItemHandler().RemoveFurniture(null, item.Id);
                continue;
            }
            if (item.Definition.InteractionType == InteractionType.Moodlight)
            {
                using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
                dbClient.SetQuery("DELETE FROM `room_items_moodlight` WHERE `item_id` = @itemId LIMIT 1");
                dbClient.AddParameter("itemId", item.Id);
                dbClient.RunQuery();
            }
            // Its database row is moved to the inventory (room 0) by
            // SellApartmentBack; an owner online gets it in their open inventory now.
            var owner = clients.GetClientByUserId((int)item.UserId);
            room.GetRoomItemHandler().RemoveFurniture(owner, item.Id);
            if (owner?.GetHabbo() != null)
            {
                owner.GetHabbo().Inventory.Furniture.AddItem(item.ToInventoryItem());
                owner.Send(new FurniListUpdateComposer());
            }
        }
    }

    /// <summary>Put the apartment on the real estate board at this asking price, or change the price.</summary>
    private static string ListForSale(GameClient session, int price)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;
        if (price < 1)
            return "Type a price first.";
        if (price > MaxListPrice)
            return $"The most you can ask is {Money(MaxListPrice)}.";
        if (habbo.Credits < ListingFee)
            return $"Listing costs {Money(ListingFee)} cash in hand.";

        // The fee and the listing together, and only for an apartment not
        // already on the board - so one listing is one fee.
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var listed = connection.Execute(
            "UPDATE `rp_apartments` SET `list_price` = @price, `listed_at` = UNIX_TIMESTAMP() WHERE `user_id` = @me AND `list_price` = 0 LIMIT 1",
            new { price, me = habbo.Id }, transaction);
        if (listed != 1)
        {
            transaction.Rollback();
            return (ApartmentOf(habbo.Id) == null) ? "You don't have an apartment." : "Your apartment is already for sale.";
        }
        if (!TakeCash(connection, transaction, habbo.Id, ListingFee))
        {
            transaction.Rollback();
            return $"Listing costs {Money(ListingFee)} cash in hand.";
        }
        transaction.Commit();

        habbo.ChangeCreditsInMemory(-ListingFee);
        CoinLedger.Record(habbo, -ListingFee, "Apartments: listing fee");
        session.Send(new CreditBalanceComposer(habbo.Credits));
        return $"Your apartment is on the real estate board for {Money(price)}.";
    }

    /// <summary>Take the apartment off the real estate board.</summary>
    private static string TakeOffSale(GameClient session)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "UPDATE `rp_apartments` SET `list_price` = 0, `listed_at` = 0 WHERE `user_id` = @me LIMIT 1",
            new { me = session.GetHabbo().Id });
        return "Your apartment is off the real estate board.";
    }

    /// <summary>Take this much cash from them in the transaction - only if they still hold it.</summary>
    internal static bool TakeCash(IDbConnection connection, IDbTransaction transaction, int userId, int amount) =>
        connection.Execute(
            "UPDATE `users` SET `credits` = `credits` - @amount WHERE `id` = @me AND `credits` >= @amount LIMIT 1",
            new { amount, me = userId }, transaction) == 1;

    /// <summary>
    /// Off to their apartment, from the point - a room change the server
    /// makes (RoomTransfers), arriving at the door, on its arrow. Null when
    /// they are on their way: the room change closes the window.
    /// </summary>
    private static string? GoToApartment(GameClient session, Room from)
    {
        var habbo = session.GetHabbo();
        var refusal = Refusal(session);
        if (refusal != null)
            return refusal;
        var apartment = ApartmentOf(habbo.Id);
        if (apartment == null || apartment.RoomId == 0)
            return "You don't have an apartment yet.";
        if (!RoomTransfers.Start(habbo, from, null,
                () =>
                {
                    if (session.GetHabbo() == habbo && habbo.CurrentRoom == from)
                        habbo.PrepareRoom(apartment.RoomId, "", lockOldRoom: true);
                }, null))
            return "You are already on your way somewhere.";
        return null;
    }

    /// <summary>
    /// A step onto an apartment's arrow. Like an arrow, only a walk that ENDS
    /// on it (its goal) takes them out - crossing it on the way somewhere else
    /// does not. The way back is read from the database, so - as
    /// <see cref="OpenSoon"/> - it runs on RoomTickWriter's thread, for
    /// somebody still standing on it by then.
    /// </summary>
    public static void LeaveSoon(RoomUser user, Item exit)
    {
        var client = user?.GetClient();
        var habbo = client?.GetHabbo();
        if (habbo == null || user!.IsBot || user.GoalX != exit.GetX || user.GoalY != exit.GetY)
            return;
        var roomId = exit.RoomId;
        int x = exit.GetX, y = exit.GetY;
        RoomTickWriter.QueueOrdered(() =>
        {
            if (client!.GetHabbo() != habbo)
                return;
            var room = habbo.CurrentRoom;
            if (room == null || room.RoomId != roomId)
                return;
            var here = room.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
            if (here == null || here.X != x || here.Y != y)
                return;
            uint lobby;
            using (var connection = PlusEnvironment.DatabaseManager.Connection())
                lobby = connection.ExecuteScalar<uint?>("SELECT `lobby_room_id` FROM `rp_apartments` WHERE `room_id` = @room LIMIT 1", new { room = roomId }) ?? 0;
            if (lobby == 0 || lobby == roomId)
            {
                client!.SendWhisper("This door doesn't lead anywhere.");
                return;
            }
            // Their own apartment: its picture for the real estate board is
            // taken as they walk out (Twist) - asked for before the trip, so
            // the client draws the room before it leaves it.
            if (room.OwnerId == habbo.Id)
            {
                Snapshots[habbo.Id] = (roomId, Environment.TickCount64);
                client!.Send(new InClientLinkComposer("apartments/snapshot"));
            }
            RoomTransfers.Start(habbo, room, null,
                () =>
                {
                    if (client!.GetHabbo() == habbo && habbo.CurrentRoom == room)
                        habbo.PrepareRoom(lobby, "", lockOldRoom: true);
                }, null);
        });
    }

    /// <summary>This player's apartment, or null.</summary>
    internal static ApartmentRow? ApartmentOf(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<ApartmentRow>(
            "SELECT a.`room_id` AS RoomId, a.`lots` AS Lots, a.`lobby_room_id` AS LobbyRoomId, a.`paid` AS Paid, a.`bought_at` AS BoughtAt, " +
            "a.`list_price` AS ListPrice, COALESCE(r.`caption`, '') AS RoomName, COALESCE(r.`model_name`, '') AS ModelName " +
            "FROM `rp_apartments` a LEFT JOIN `rooms` r ON r.`id` = a.`room_id` WHERE a.`user_id` = @me LIMIT 1",
            new { me = userId });
    }

    /// <summary>The lots an apartment covers, from `rp_apartments.lots`.</summary>
    public static List<int> LotsOf(ApartmentRow? apartment)
    {
        if (apartment == null)
            return new();
        return apartment.Lots.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var lot) ? lot : -1)
            .Where(lot => lot >= 0 && lot < Grid * Grid)
            .Distinct()
            .ToList();
    }

    /// <summary>An apartment on every lot of the grid is a Penthouse (Twist, 2026-10-28) - its room is renamed so when the last lot is bought.</summary>
    public static bool IsPenthouse(IReadOnlyCollection<int> lots) => lots.Count >= Grid * Grid;

    /// <summary>Whether <paramref name="lot"/> shares a side with one of these - up, down, left or right; corners do not count (Twist).</summary>
    public static bool IsNextTo(IReadOnlyCollection<int> lots, int lot)
    {
        int x = lot % Grid, y = lot / Grid;
        return (x > 0 && lots.Contains(lot - 1))
               || (x < Grid - 1 && lots.Contains(lot + 1))
               || (y > 0 && lots.Contains(lot - Grid))
               || (y < Grid - 1 && lots.Contains(lot + Grid));
    }

    /// <summary>
    /// The floor plan of an apartment covering these lots. The room is always
    /// the whole grid's size, with a void row and column on the top and left,
    /// so a lot's tiles are the same whichever lots are bought: lot (lx, ly)
    /// is columns 1 + lx * LotSize on, rows 1 + ly * LotSize on. The door is a
    /// tile sticking out of the west wall, halfway down the westernmost lot
    /// (the one nearest the middle row, if there are several). Rows are split
    /// by \r and end in a void row, as the floor plan editor saves them.
    /// </summary>
    public static (string Map, int DoorX, int DoorY) FloorPlan(IReadOnlyCollection<int> lots)
    {
        var size = 1 + Grid * LotSize;
        var rows = Enumerable.Range(0, size).Select(_ => Enumerable.Repeat('x', size).ToArray()).ToArray();
        foreach (var lot in lots)
        {
            int left = 1 + (lot % Grid) * LotSize, top = 1 + (lot / Grid) * LotSize;
            for (var y = 0; y < LotSize; y++)
            for (var x = 0; x < LotSize; x++)
                rows[top + y][left + x] = '0';
        }
        var west = lots.Min(lot => lot % Grid);
        var doorLot = lots.Where(lot => lot % Grid == west).OrderBy(lot => Math.Abs(lot / Grid - Grid / 2)).First();
        var doorX = west * LotSize;
        var doorY = 1 + (doorLot / Grid) * LotSize + LotSize / 2;
        rows[doorY][doorX] = '0';
        var map = string.Join("\r", rows.Select(row => new string(row)));
        return (map + "\r" + new string('x', size), doorX, doorY);
    }
}
