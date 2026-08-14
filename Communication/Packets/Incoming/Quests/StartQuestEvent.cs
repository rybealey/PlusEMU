using Plus.Communication.Packets.Outgoing.Quests;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;

namespace Plus.Communication.Packets.Incoming.Quests;

internal class StartQuestEvent : IPacketEvent
{
    private readonly IQuestManager _questManager;
    private readonly IDatabase _database;

    public StartQuestEvent(IQuestManager questManager, IDatabase database)
    {
        _questManager = questManager;
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var questId = packet.ReadInt();
        var quest = _questManager.GetQuest(questId);
        if (quest == null)
            return Task.CompletedTask;
        // Restarting a finished quest leaves its stored progress at or above the goal, so the
        // very next progress tick completes it again and pays the reward again. Sending this
        // packet in a loop with any qualifying action farms Duckets without limit.
        if (session.GetHabbo().GetQuestProgress(quest.Id) >= quest.GoalData)
            return Task.CompletedTask;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.RunQuery($"REPLACE INTO `user_quests` (`user_id`,`quest_id`) VALUES ('{session.GetHabbo().Id}', '{quest.Id}')");
            dbClient.RunQuery($"UPDATE `user_statistics` SET `quest_id` = '{quest.Id}' WHERE `id` = '{session.GetHabbo().Id}' LIMIT 1");
        }
        session.GetHabbo().HabboStats.QuestId = quest.Id;
        _questManager.GetList(session, null);
        session.Send(new QuestStartedComposer(session, quest));
        return Task.CompletedTask;
    }
}