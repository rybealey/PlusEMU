using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: one staff action on a player. Restore, kill and summon
/// run the very commands staff type (:restore, :kill, :summon), in the target's
/// room so their HUD hears it; the rest are the panel's own. Each needs its own
/// capability (CityPanelAccess), none works on the staff member's own
/// characters, and each is logged like a staff command.
/// </summary>
internal class RpCityPlayerActionEvent : IPacketEvent
{
    public const int Restore = 1;
    public const int Kill = 2;
    public const int Summon = 3;
    public const int GoTo = 4;
    public const int Release = 5;
    public const int ClearCharges = 6;
    public const int AdjustBalance = 7;

    /// <summary>The most a single balance change may move, either way.</summary>
    private const int MaxBalanceChange = 1_000_000;

    private readonly ICommandManager _commandManager;

    public RpCityPlayerActionEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var action = packet.ReadInt();
        var amount = packet.ReadInt();
        var staff = session.GetHabbo();
        if (!CityPanelAccess.CanOpen(staff))
            return Task.CompletedTask;

        var notice = Run(session, staff!, userId, action, amount);
        if (notice == null)
            _commandManager.LogCommand(staff!.Id, $"city-panel action={action} user={userId} amount={amount}", staff.MachineId);

        var card = CityPlayers.Card(userId);
        if (card != null)
            session.Send(new RpCityPlayerComposer(card, notice ?? ""));
        return Task.CompletedTask;
    }

    /// <summary>Carry the action out. What to tell the staff member, or null when it went through.</summary>
    private string? Run(GameClient session, Habbo staff, int userId, int action, int amount)
    {
        var required = action switch
        {
            Restore => CityPanelAccess.Capability.Restore,
            Kill => CityPanelAccess.Capability.Kill,
            Summon => CityPanelAccess.Capability.Summon,
            GoTo => CityPanelAccess.Capability.GoTo,
            Release or ClearCharges => CityPanelAccess.Capability.Justice,
            AdjustBalance => CityPanelAccess.Capability.Balance,
            _ => CityPanelAccess.Capability.None
        };
        if (required == CityPanelAccess.Capability.None)
            return "Unknown action.";
        if (!CityPanelAccess.Can(staff, required))
            return "You don't have permission to do that.";
        if (CityPlayers.IsOwnCharacter(staff, userId))
            return "That is one of your own characters.";

        var targetClient = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var target = targetClient?.GetHabbo();

        switch (action)
        {
            case Restore:
                if (target == null)
                {
                    CityPlayers.RestoreOffline(userId);
                    return null;
                }
                return RunCommand(session, "restore", target, target.CurrentRoom) ? null : "Could not restore them.";
            case Kill:
                if (target?.CurrentRoom == null)
                    return "They need to be online and in a room.";
                return RunCommand(session, "kill", target, target.CurrentRoom) ? null : "Could not knock them out.";
            case Summon:
                if (target == null)
                    return "They are offline.";
                if (staff.CurrentRoom == null)
                    return "You need to be in a room to summon somebody to it.";
                return RunCommand(session, "summon", target, staff.CurrentRoom) ? null : "Could not summon them.";
            case GoTo:
                if (target?.CurrentRoom == null)
                    return "They are not in a room.";
                if (staff.CurrentRoom?.RoomId == target.CurrentRoom.RoomId)
                    return "You are already in their room.";
                staff.PrepareRoom(target.CurrentRoom.RoomId, "");
                return null;
            case Release:
                if (!JailState.StaffRelease(userId, targetClient))
                    return "They are not in jail.";
                targetClient?.SendWhisper("A staff member has released you from jail.");
                return null;
            case ClearCharges:
                WantedUtility.ExpireLapsed();
                if (WantedUtility.DropAll(userId) == 0)
                    return "They have nothing on their record.";
                WantedUtility.Broadcast();
                return null;
            case AdjustBalance:
                if (amount == 0 || Math.Abs(amount) > MaxBalanceChange)
                    return $"Change the balance by 1 to {MaxBalanceChange:N0} coins.";
                CityPlayers.AdjustCredits(userId, amount, staff.Username);
                return null;
        }
        return "Unknown action.";
    }

    private bool RunCommand(GameClient session, string key, Habbo target, HabboHotel.Rooms.Room? room)
    {
        if (room == null || !_commandManager.TryGetCommand(key, out var command) || command is not ITargetChatCommand targetCommand)
            return false;
        targetCommand.Execute(session, room, target, Array.Empty<string>());
        return true;
    }
}
