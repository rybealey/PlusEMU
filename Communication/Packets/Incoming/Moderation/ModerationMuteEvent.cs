using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal class ModerationMuteEvent : IPacketEvent
{
    public readonly IDatabase _database;

    public ModerationMuteEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().Permissions.HasRight("mod_mute"))
            return Task.CompletedTask;
        var userId = packet.ReadInt();
        var message = packet.ReadString();
        packet.ReadInt(); // cfh topic id — the mod-tools mute sanction is a fixed 1 hour
        double length = 3600;
        var habbo = PlusEnvironment.GetHabboById(userId);
        if (habbo == null)
        {
            session.SendWhisper("An error occoured whilst finding that user in the database.");
            return Task.CompletedTask;
        }
        if (habbo.Permissions.HasRight("mod_mute") && !session.GetHabbo().Permissions.HasRight("mod_mute_any"))
        {
            session.SendWhisper("Oops, you cannot mute that user.");
            return Task.CompletedTask;
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.RunQuery($"UPDATE `users` SET `time_muted` = '{length}' WHERE `id` = '{habbo.Id}' LIMIT 1");
        }
        if (habbo.Client != null)
        {
            habbo.TimeMuted = length;
            habbo.Client.SendModerationAlert(string.IsNullOrWhiteSpace(message)
                ? $"You have been muted by a moderator for {length} seconds!"
                : $"You have been muted by a moderator for {length} seconds!\r\rReason:\r\r{message}");
        }
        return Task.CompletedTask;
    }
}