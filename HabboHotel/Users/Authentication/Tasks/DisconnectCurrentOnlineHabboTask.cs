using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Authentication.Tasks;

public class DisconnectCurrentOnlineHabboTask : IAuthenticationTask
{
    private readonly IGameClientManager _gameClientManager;

    public DisconnectCurrentOnlineHabboTask(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task<bool> CanLogin(int userId)
    {
        var existingSession = _gameClientManager.GetClientByUserId(userId);
        if (existingSession == null)
            return Task.FromResult(true);
        // pixelrp: tell the old window why before closing it, so its
        // disconnected screen says "signed in elsewhere", not "connection lost".
        try { existingSession.Send(new RpDisconnectReasonComposer(RpDisconnectReasonComposer.ReasonReplaced, "duplicate login")); }
        catch (Exception) { }
        existingSession.Disconnect();
        return Task.FromResult(true);
    }
}