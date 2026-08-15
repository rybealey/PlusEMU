using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Achievements;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Database;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Messenger;
using Plus.HabboHotel.Users;
using Plus.Core;

namespace Plus.HabboHotel.Achievements;

public class AchievementManager : IAchievementManager, IStartable
{
    public Dictionary<string, Achievement> Achievements { get; private set; }
    private readonly IAchievementLevelFactory _achievementLevelFactory;
    private readonly IDatabase _database;
    private readonly IBadgeManager _badgeManager;

    public AchievementManager(IAchievementLevelFactory achievementLevelFactory, IDatabase database, IBadgeManager badgeManager)
    {
        _achievementLevelFactory = achievementLevelFactory;
        _database = database;
        _badgeManager = badgeManager;
        Achievements = new();
    }

    public async Task Start() => await Init();

    public async Task Init() => Achievements = await _achievementLevelFactory.GetAchievementLevels();

    public bool ProgressAchievement(GameClient session, string group, int progress, bool fromBeginning = false)
    {
        // The achievement system is disabled hotel-wide. This is the single
        // choke point every ProgressAchievement call site routes through, so
        // returning here stops all progress, unlocks, badge grants and
        // pixel/point payouts without touching the ~39 callers.
        return false;
    }

    public ICollection<Achievement> GetGameAchievements(int gameId)
    {
        var achievements = new List<Achievement>();
        foreach (var achievement in Achievements.Values.ToList())
        {
            if (achievement.Category == "games" && achievement.GameId == gameId)
                achievements.Add(achievement);
        }
        return achievements;
    }

    public void BroadcastAchievement(Habbo habbo, MessengerEventTypes eventType, string level)
    {
    }
}