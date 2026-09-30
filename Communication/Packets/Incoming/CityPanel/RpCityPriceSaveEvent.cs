using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: set a service's price. Logged like a staff command.</summary>
internal class RpCityPriceSaveEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityPriceSaveEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var key = packet.ReadString() ?? "";
        var price = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;

        var notice = ServicePrices.Set(key, price, habbo!.Id) ? "Price saved." : "That service no longer exists.";
        _commandManager.LogCommand(habbo.Id, $"city-panel price {key}={price}", habbo.MachineId);
        session.Send(new RpCityEconomyComposer(CityEconomy.Corporations(), ServicePrices.All(), notice));
        return Task.CompletedTask;
    }
}
