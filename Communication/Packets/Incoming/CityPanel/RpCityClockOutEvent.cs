using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: end somebody's shift - the same clock-out :stopwork
/// gives them (their pay so far is kept, their uniform comes off). Logged like
/// a staff command.
/// </summary>
internal class RpCityClockOutEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityClockOutEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Shifts))
            return Task.CompletedTask;

        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        string notice;
        if (client?.GetHabbo() == null || !ShiftManager.IsOnDuty(userId))
            notice = "They are not on shift.";
        else
        {
            ShiftManager.StopShift(client);
            client.SendWhisper("A staff member has clocked you out.");
            _commandManager.LogCommand(habbo!.Id, $"city-panel clock-out user={userId}", habbo.MachineId);
            notice = $"{client.GetHabbo().Username} is off shift.";
        }
        session.Send(new RpCityEconomyComposer(CityEconomy.Corporations(), ServicePrices.All(), notice));
        return Task.CompletedTask;
    }
}
