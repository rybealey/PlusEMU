using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: set a rank's pay (coins per ten minutes on shift). Takes
/// effect from the next payday of everyone on duty at it. Logged like a staff
/// command.
/// </summary>
internal class RpCityPaySaveEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityPaySaveEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var rankId = packet.ReadInt();
        var pay = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;

        var notice = CityEconomy.SetPay(rankId, pay) ? "Pay saved. It counts from the next payday." : "That rank no longer exists.";
        _commandManager.LogCommand(habbo!.Id, $"city-panel pay rank={rankId} pay={pay}", habbo.MachineId);
        session.Send(new RpCityEconomyComposer(CityEconomy.Corporations(), ServicePrices.All(), notice));
        return Task.CompletedTask;
    }
}
