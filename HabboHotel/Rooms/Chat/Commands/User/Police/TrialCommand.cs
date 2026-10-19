using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp: :trial - a prisoner asks for a jury trial, as the Request a Trial
/// card does (Twist, 2026-10-20). Everything is checked in
/// JailState.RequestTrial; typed, each refusal is whispered, not enough
/// players online included.
/// </summary>
internal class TrialCommand : IChatCommand
{
    public string Key => "trial";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Request a jury trial while in jail.";

    public void Execute(GameClient session, Room room, string[] parameters) =>
        JailState.RequestTrial(session, typed: true);
}
