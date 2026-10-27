using Plus.Communication.Packets.Outgoing.Inventory.Bots;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Bots;

internal class PickUpBotEvent : IPacketEvent
{
    public readonly IDatabase _database;

    public PickUpBotEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var botId = packet.ReadInt();
        if (botId == 0)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.GetRoomUserManager().TryGetBot(botId, out var botUser))
            return Task.CompletedTask;
        // pixelrp: a :zombies stand-in has no row to own - it is not picked up
        // into anyone's bots (bare :zombies takes it away)
        if (HabboHotel.Rooms.Chat.Commands.Moderator.ZombiesCommand.IsZombie(botUser))
            return Task.CompletedTask;
        // pixelrp: in a PixelRP room, a bot a staff member picks up becomes
        // theirs, like furni there (PixelRpRooms).
        var staffTakesIt = PixelRpRooms.StaffTakesIt(room, session);
        if (session.GetHabbo().Id != botUser.BotData.OwnerId && !session.GetHabbo().Permissions.HasRight("bot_place_any_override") && !staffTakesIt)
        {
            session.SendWhisper("You can only pick up your own bots!");
            return Task.CompletedTask;
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery(staffTakesIt
                ? "UPDATE `bots` SET `room_id` = '0', `user_id` = @owner WHERE `id` = @id LIMIT 1"
                : "UPDATE `bots` SET `room_id` = '0' WHERE `id` = @id LIMIT 1");
            dbClient.AddParameter("id", botId);
            if (staffTakesIt)
                dbClient.AddParameter("owner", session.GetHabbo().Id);
            dbClient.RunQuery();
        }
        if (staffTakesIt)
            botUser.BotData.OwnerId = session.GetHabbo().Id;
        session.GetHabbo().Inventory.Bots.AddBot(new(Convert.ToInt32(botUser.BotData.Id), Convert.ToInt32(botUser.BotData.OwnerId), botUser.BotData.Name, botUser.BotData.Motto,
            botUser.BotData.Look, botUser.BotData.Gender));
        session.Send(new BotInventoryComposer(session.GetHabbo().Inventory.Bots.Bots.Values.ToList()));
        room.GetRoomUserManager().RemoveBot(botUser.VirtualId, false);
        // After the removal, so the set the client gets no longer holds a
        // virtual id nothing answers to.
        room.GetRoomUserManager().BroadcastTellerBots();
        room.GetRoomUserManager().BroadcastGymBots();
        return Task.CompletedTask;
    }
}