using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: one of a corporation's settings in the Economy tab - a
/// corporation id, a setting (CityEconomy.Setting*) and its value. For now the
/// one setting is Hide corporation: 1 hides it from the Corporations window for
/// everybody, 0 shows it again. Logged like a staff command.
/// </summary>
internal class RpCityCorpSettingEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityCorpSettingEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var corpId = packet.ReadInt();
        var setting = packet.ReadInt();
        var value = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Economy))
            return Task.CompletedTask;

        string notice;
        if (!CityEconomy.SetCorpSetting(corpId, setting, value))
            notice = "That setting could not be changed.";
        else
        {
            _commandManager.LogCommand(habbo!.Id, $"city-panel corp-setting corp={corpId} setting={setting} value={value}", habbo.MachineId);
            notice = value == 1 ? "Hidden from the Corporations window." : "Shown in the Corporations window again.";
        }
        session.Send(new RpCityEconomyComposer(CityEconomy.Corporations(), ServicePrices.All(), notice));
        return Task.CompletedTask;
    }
}
