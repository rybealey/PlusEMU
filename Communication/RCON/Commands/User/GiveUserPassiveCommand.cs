using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.RCON.Commands.User;

/// <summary>
/// pixelrp: grants (or extends to at least) a passive window for an online
/// player, e.g. while they have the Diamonds Store payment form open. Unlike
/// the smoothie there is deliberately NO room announcement - the only visible
/// change is the passive tag via the stats broadcast.
/// </summary>
internal class GiveUserPassiveCommand : IRconCommand
{
    private readonly IGameClientManager _gameClientManager;
    public string Description => "Silently grant a user passive status for at least the given number of seconds.";

    public string Key => "give_user_passive";
    public string Parameters => "%userId% %seconds%";

    public GiveUserPassiveCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task<bool> TryExecute(string[] parameters)
    {
        if (parameters == null || parameters.Length < 2)
            return Task.FromResult(false);
        if (!int.TryParse(parameters[0], out var userId) || !int.TryParse(parameters[1], out var seconds) || seconds <= 0)
            return Task.FromResult(false);

        var habbo = _gameClientManager.GetClientByUserId(userId)?.GetHabbo();
        if (habbo == null)
            return Task.FromResult(false);

        habbo.EnsureRpStatsLoaded();
        // Never shorten a longer passive the player already has (smoothie).
        habbo.RpPassiveSeconds = Math.Max(habbo.RpPassiveSeconds, seconds);
        habbo.RpPassiveLastTick = 0;
        habbo.SaveRpStats();

        var roomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (roomUser != null)
            habbo.CurrentRoom.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 1));
        return Task.FromResult(true);
    }
}
