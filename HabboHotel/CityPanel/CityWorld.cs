using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Weather;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp City Panel: the City tab - the hotel alert, the sky, maintenance and
/// the combat switch. Switches live in server_settings, so a restart keeps
/// them. A missing key reads as "0" (SettingsManager), so every key is named so
/// that 0 is the normal hotel: `hotel.combat_paused`, not `hotel.combat`.
/// </summary>
public static class CityWorld
{
    private const string MaintenanceKey = "hotel.maintenance";
    private const string CombatPausedKey = "hotel.combat_paused";

    /// <summary>How long players are warned before maintenance takes them offline.</summary>
    public const int MaintenanceWarningSeconds = 60;

    /// <summary>Only staff (account rank 5+) may be online during maintenance.</summary>
    public const int MaintenanceStaffRank = 5;

    /// <summary>What fighting means to the combat switch: every command that hurts, stuns or shoves.</summary>
    private static readonly HashSet<string> CombatCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "hit", "slap", "spit", "stun", "fb", "ps", "push", "spush", "pull"
    };

    public static bool IsMaintenance => PlusEnvironment.SettingsManager.TryGetValue(MaintenanceKey) == "1";

    public static bool IsCombatPaused => PlusEnvironment.SettingsManager.TryGetValue(CombatPausedKey) == "1";

    /// <summary>Whether the combat switch stops this command. One gate in CommandManager.</summary>
    public static bool CombatBlocks(string commandKey) => IsCombatPaused && CombatCommands.Contains(commandKey);

    public static void SetCombatPaused(bool paused) => PlusEnvironment.SettingsManager.Set(CombatPausedKey, paused ? "1" : "0");

    /// <summary>
    /// Maintenance on: logins are refused below staff (SSOTicketEvent), everyone
    /// online is warned, and anybody not staff still online a minute later is
    /// taken off. Off: the doors open again.
    /// </summary>
    public static void SetMaintenance(bool on)
    {
        PlusEnvironment.SettingsManager.Set(MaintenanceKey, on ? "1" : "0");
        if (!on)
            return;
        Alert($"The hotel is going into maintenance. You will be disconnected in {MaintenanceWarningSeconds} seconds.");
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(MaintenanceWarningSeconds));
            // Switched off again in the meantime: nobody goes.
            if (!IsMaintenance)
                return;
            foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
            {
                var habbo = client?.GetHabbo();
                if (habbo != null && AccountUtility.AccountRank(habbo.Id) < MaintenanceStaffRank)
                    client!.Disconnect();
            }
        });
    }

    /// <summary>The hotel alert - the same notification bubble :ha sends.</summary>
    public static RoomNotificationComposer AlertComposer(string message) =>
        new("hotel.alert", new Dictionary<string, string> { { "display", "BUBBLE" }, { "message", message } });

    public static void Alert(string message) => PlusEnvironment.Game.ClientManager.SendPacket(AlertComposer(message));

    /// <summary>The alert, to staff only (rank 5+).</summary>
    public static void AlertStaff(string message)
    {
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
            if (client?.GetHabbo() != null && client.GetHabbo().Rank >= 5)
                client.Send(AlertComposer(message));
    }

    /// <summary>The Right now row: who is online, clocked in, wanted and serving.</summary>
    public static (int Online, int OnDuty, int Wanted, int Jailed) Counts()
    {
        WantedUtility.ExpireLapsed();
        return (PlusEnvironment.Game.ClientManager.GetClients.Count(client => client?.GetHabbo() != null),
            ShiftManager.OnDutyCount, WantedUtility.GetWanted().Count, JailState.ServingCount);
    }

    /// <summary>Everything the City tab shows.</summary>
    public sealed class State
    {
        public int OverrideCode { get; init; }
        public int LiveCode { get; init; }
        public int PinnedMinutes { get; init; }
        public bool Maintenance { get; init; }
        public bool CombatPaused { get; init; }
        public int Online { get; init; }
        public int OnDuty { get; init; }
        public int Wanted { get; init; }
        public int Jailed { get; init; }
    }

    public static State Current()
    {
        var (online, onDuty, wanted, jailed) = Counts();
        return new State
        {
            OverrideCode = WeatherStation.OverrideCode,
            LiveCode = WeatherStation.LiveCode,
            PinnedMinutes = WeatherStation.PinnedMinutes,
            Maintenance = IsMaintenance,
            CombatPaused = IsCombatPaused,
            Online = online,
            OnDuty = onDuty,
            Wanted = wanted,
            Jailed = jailed
        };
    }
}
