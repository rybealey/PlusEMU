using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: add a Bin Search reward (id 0) or change one. Money,
/// so the Economy permission, like prices. Logged like a staff command.
/// </summary>
internal class RpCityBinRewardSaveEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityBinRewardSaveEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var key = packet.ReadString() ?? "";
        var weight = packet.ReadInt();
        var min = packet.ReadInt();
        var max = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;

        var refusal = BinRewards.Save(id, key, weight, min, max, habbo!.Id);
        if (refusal == null)
            _commandManager.LogCommand(habbo.Id, $"city-panel bin reward {(id > 0 ? id.ToString() : "new")} {key} weight={weight} {min}-{max}", habbo.MachineId);
        session.Send(new RpCityBinRewardsComposer(refusal ?? (id > 0 ? "Reward saved." : "Reward added.")));
        return Task.CompletedTask;
    }
}
