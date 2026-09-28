using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :claim - start taking this turf for your gang.
///
/// The claim is a hold, not a click: TurfManager runs it for
/// turf.capture.seconds and it completes only while nobody from another gang is
/// in the room and the claimer stays, conscious and uncuffed. Everything about
/// WHO may start one is checked here; everything about how it runs, there.
/// </summary>
internal class ClaimCommand : IChatCommand
{
    public string Key => "claim";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Claim this turf for your gang.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (!room.IsTurf)
        {
            session.SendWhisper("This room isn't a turf.");
            return;
        }
        if (KnockedOut.Refuse(session))
            return;
        if (Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }

        var gang = GangUtility.GetGang(habbo.Id);
        if (gang == null)
        {
            session.SendWhisper("You're not in a gang.");
            return;
        }
        if (TurfManager.OwnerOf(room.RoomId) == gang.GangId)
        {
            session.SendWhisper("Your gang already holds this turf.");
            return;
        }

        // A turf is an unsafe room, and a passive player is one nobody can
        // fight - so they cannot be the one holding it either.
        habbo.EnsureRpStatsLoaded();
        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You can't claim turf while you're passive.");
            return;
        }

        if (!TurfManager.TryStart(room, habbo, gang))
        {
            TurfManager.IsCapturing(room.RoomId, out var claiming);
            session.SendWhisper($"{claiming} is already claiming this turf.");
        }
    }
}
