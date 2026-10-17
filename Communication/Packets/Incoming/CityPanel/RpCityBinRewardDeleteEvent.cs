using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: take a Bin Search reward out. Economy permission; logged.</summary>
internal class RpCityBinRewardDeleteEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityBinRewardDeleteEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;

        var removed = BinRewards.Delete(id);
        if (removed)
            _commandManager.LogCommand(habbo!.Id, $"city-panel bin reward {id} removed", habbo.MachineId);
        session.Send(new RpCityBinRewardsComposer(removed ? "Reward removed." : "That reward no longer exists."));
        return Task.CompletedTask;
    }
}
