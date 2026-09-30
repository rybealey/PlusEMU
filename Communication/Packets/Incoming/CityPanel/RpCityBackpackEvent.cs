using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: change a player's backpack - give an item, empty a slot,
/// or set a stack's count (CityPlayers.Backpack*). Needs :spawn's permission,
/// since giving is what :spawn does. Logged like a staff command.
/// </summary>
internal class RpCityBackpackEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityBackpackEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var op = packet.ReadInt();
        var slot = packet.ReadInt();
        var item = packet.ReadString() ?? "";
        var count = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Backpack))
            return Task.CompletedTask;

        string? notice;
        if (CityPlayers.IsOwnCharacter(habbo!, userId))
            notice = "That is one of your own characters.";
        else
        {
            notice = CityPlayers.EditBackpack(userId, op, slot, item, count);
            if (notice == null)
                _commandManager.LogCommand(habbo!.Id, $"city-panel backpack {userId} op={op} slot={slot} item={item} count={count}", habbo.MachineId);
        }

        var card = CityPlayers.Card(userId);
        if (card != null)
            session.Send(new RpCityPlayerComposer(card, notice ?? ""));
        return Task.CompletedTask;
    }
}
