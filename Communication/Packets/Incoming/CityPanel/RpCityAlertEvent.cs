using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: the hotel alert - to everyone (:ha's permission), to
/// staff (:sa's) or to the room the staff member is in (:ra's). Logged like a
/// staff command.
/// </summary>
internal class RpCityAlertEvent : IPacketEvent
{
    public const int Everyone = 0;
    public const int Staff = 1;
    public const int ThisRoom = 2;

    private const int MaxLength = 500;

    private readonly ICommandManager _commandManager;

    public RpCityAlertEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var target = packet.ReadInt();
        var message = (packet.ReadString() ?? "").Trim();
        var habbo = session.GetHabbo();
        if (message.Length == 0)
            return Task.CompletedTask;
        if (message.Length > MaxLength)
            message = message[..MaxLength];

        var required = target switch
        {
            Everyone => CityPanelAccess.Capability.AlertHotel,
            Staff => CityPanelAccess.Capability.AlertStaff,
            ThisRoom => CityPanelAccess.Capability.AlertRoom,
            _ => CityPanelAccess.Capability.None
        };
        if (required == CityPanelAccess.Capability.None || !CityPanelAccess.Can(habbo, required))
            return Task.CompletedTask;

        string notice;
        switch (target)
        {
            case Everyone:
                CityWorld.Alert(message);
                notice = "Alert sent to everyone.";
                break;
            case Staff:
                CityWorld.AlertStaff(message);
                notice = "Alert sent to staff.";
                break;
            default:
                var room = habbo!.CurrentRoom;
                if (room == null)
                {
                    session.Send(new RpCityWorldComposer(CityWorld.Current(), "You need to be in a room to alert it."));
                    return Task.CompletedTask;
                }
                room.SendPacket(CityWorld.AlertComposer(message));
                notice = "Alert sent to this room.";
                break;
        }

        _commandManager.LogCommand(habbo!.Id, $"city-panel alert target={target} {message}", habbo.MachineId);
        session.Send(new RpCityWorldComposer(CityWorld.Current(), notice));
        return Task.CompletedTask;
    }
}
