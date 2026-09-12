using System.Data;
using Plus.Communication.Packets.Outgoing.Inventory.Bots;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Bots;

internal class PlaceBotEvent : RoomPacketEvent
{
    /// <summary>
    /// pixelrp: the facing a bot lands in, named rather than left as a literal.
    /// It is the rotation this handler has always used - towards the room - and
    /// it is now written to the row as well as to the bot.
    /// </summary>
    private const int PlacementRotation = 4;

    private readonly IDatabase _database;

    public PlaceBotEvent(IDatabase database)
    {
        _database = database;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        var botId = packet.ReadInt();
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        if (!room.GetGameMap().CanWalk(x, y, false) || !room.GetGameMap().ValidTile(x, y))
        {
            session.SendNotification("You cannot place a bot here!");
            return Task.CompletedTask;
        }
        if (!session.GetHabbo().Inventory.Bots.Bots.TryGetValue(botId, out var bot))
            return Task.CompletedTask;
        var botCount = 0;
        foreach (var user in room.GetRoomUserManager().GetUserList().ToList())
        {
            if (user == null || user.IsPet || !user.IsBot)
                continue;
            botCount += 1;
        }
        if (botCount >= 5 && !session.GetHabbo().Permissions.HasRight("bot_place_any_override"))
        {
            session.SendNotification("Sorry; 5 bots per room only!");
            return Task.CompletedTask;
        }

        //TODO: Hmm, maybe not????
        using (var dbClient = _database.GetQueryReactor())
        {
            // pixelrp: the facing is written here too. DeployBot below poses a
            // freshly placed bot at PlacementRotation, and if the row keeps
            // whatever it said before, a restart before the room next unloads
            // turns the bot to face something else entirely. Row and bot agree
            // from the moment it lands; :rot is what changes it afterwards.
            dbClient.SetQuery("UPDATE `bots` SET `room_id` = @roomId, `x` = @CoordX, `y` = @CoordY, `rotation` = @Rotation WHERE `id` = @BotId LIMIT 1");
            dbClient.AddParameter("roomId", room.RoomId);
            dbClient.AddParameter("BotId", bot.Id);
            dbClient.AddParameter("CoordX", x);
            dbClient.AddParameter("CoordY", y);
            dbClient.AddParameter("Rotation", PlacementRotation);
            dbClient.RunQuery();
        }
        var botSpeechList = new List<RandomSpeech>();

        //TODO: Grab data?
        DataRow getData;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `ai_type`,`rotation`,`walk_mode`,`automatic_chat`,`speaking_interval`,`mix_sentences`,`chat_bubble` FROM `bots` WHERE `id` = @BotId LIMIT 1");
            dbClient.AddParameter("BotId", bot.Id);
            getData = dbClient.GetRow();
            dbClient.SetQuery("SELECT `text` FROM `bots_speech` WHERE `bot_id` = @BotId");
            dbClient.AddParameter("BotId", bot.Id);
            var botSpeech = dbClient.GetTable();
            foreach (DataRow speech in botSpeech.Rows) botSpeechList.Add(new(Convert.ToString(speech["text"]), bot.Id));
        }
        var botUser = room.GetRoomUserManager().DeployBot(
            new(bot.Id, room.RoomId, Convert.ToString(getData["ai_type"]), Convert.ToString(getData["walk_mode"]), bot.Name, "", bot.Figure, x, y, 0, PlacementRotation, 0, 0, 0, 0,
                ref botSpeechList, "", 0, bot.OwnerId, ConvertExtensions.EnumToBool(getData["automatic_chat"].ToString()), Convert.ToInt32(getData["speaking_interval"]),
                ConvertExtensions.EnumToBool(getData["mix_sentences"].ToString()), Convert.ToInt32(getData["chat_bubble"])), null);
        botUser.Chat("Hello!");
        // A teller only exists for the client once it knows which bots are
        // tellers, so the set is re-sent the moment one lands.
        room.GetRoomUserManager().BroadcastTellerBots();
        room.GetGameMap().UpdateUserMovement(new(x, y), new(x, y), botUser);
        if (!session.GetHabbo().Inventory.Bots.RemoveBot(botId))
        {
            Console.WriteLine($"Error whilst removing Bot: {bot.Id}");
            return Task.CompletedTask;
        }
        session.Send(new BotInventoryComposer(session.GetHabbo().Inventory.Bots.Bots.Values.ToList()));
        return Task.CompletedTask;
    }
}