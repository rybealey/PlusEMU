using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Weather;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: one City tab switch - the weather (a WMO code, -1 to
/// follow San Francisco), the time of day (minutes after midnight, -1 to follow
/// the clock), maintenance or the combat switch (0 / 1). Logged like a staff
/// command.
/// </summary>
internal class RpCityWorldSetEvent : IPacketEvent
{
    public const int Weather = 1;
    public const int Time = 2;
    public const int Maintenance = 3;
    public const int Combat = 4;

    /// <summary>The WMO codes the sky knows (client SkyModel.SkyKindOf); anything else is refused.</summary>
    private static readonly HashSet<int> WeatherCodes = new() { 0, 1, 2, 3, 45, 48, 51, 53, 55, 61, 63, 65, 71, 73, 75, 80, 81, 82, 95 };

    private readonly ICommandManager _commandManager;

    public RpCityWorldSetEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var what = packet.ReadInt();
        var value = packet.ReadInt();
        var habbo = session.GetHabbo();
        var required = what is Weather or Time ? CityPanelAccess.Capability.World : CityPanelAccess.Capability.Hotel;
        if (!CityPanelAccess.Can(habbo, required))
            return Task.CompletedTask;

        var notice = "";
        switch (what)
        {
            case Weather:
                if (value >= 0 && !WeatherCodes.Contains(value))
                    return Task.CompletedTask;
                WeatherStation.SetOverrideCode(value);
                notice = value < 0 ? "The weather follows San Francisco again." : "The weather is held for everyone.";
                break;
            case Time:
                WeatherStation.SetPinnedMinutes(value);
                notice = value < 0 ? "The sky follows the city clock again." : "The time of day is pinned for everyone.";
                break;
            case Maintenance:
                CityWorld.SetMaintenance(value == 1);
                notice = value == 1
                    ? $"Maintenance is on. Players are warned, and anyone not staff is taken offline in {CityWorld.MaintenanceWarningSeconds} seconds."
                    : "Maintenance is off. Everyone can log in again.";
                break;
            case Combat:
                CityWorld.SetCombatPaused(value == 0);
                notice = value == 0 ? "Fighting is paused city-wide." : "Fighting is back on.";
                break;
            default:
                return Task.CompletedTask;
        }

        _commandManager.LogCommand(habbo!.Id, $"city-panel world what={what} value={value}", habbo.MachineId);
        session.Send(new RpCityWorldComposer(CityWorld.Current(), notice));
        return Task.CompletedTask;
    }
}
