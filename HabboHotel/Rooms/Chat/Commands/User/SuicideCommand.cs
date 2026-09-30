using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :suicide - a player takes their own health to 0 and is knocked
/// out, exactly as a finishing punch would leave them (ApplyRpKnockout: the
/// frozen lay, and the hospital's clock starting).
///
/// Not while they have aggression: someone who has just been fighting cannot
/// drop out of the fight on their own terms. The same rounded figure the HUD
/// shows, so a player told no can see why, and one whose bar reads 0 is let
/// through.
///
/// Out cold already is answered by CommandManager's knockout gate. Cuffed and
/// jailed are too - "suicide" is on both block lists - because going down
/// ends the cuffs and the escort (PoliceState.OnKnockout), which would make
/// this the way out of an arrest.
/// </summary>
internal class SuicideCommand : IChatCommand
{
    public string Key => "suicide";

    // Every player, like :fb and :ga.
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Take your own life, knocking yourself out.";

    /// <summary>The blue action bubble every fight action uses (HitCommand.FightBubble).</summary>
    private const int ActionBubble = 4;

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || room == null)
            return;

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        habbo.EnsureRpStatsLoaded();
        if (Math.Round(habbo.RpAggression) > 0)
        {
            session.SendWhisper("You cannot do that while you have aggression.");
            return;
        }

        habbo.RpHealth = 0;
        // A medkit would otherwise go on refilling the bar and stand them back up.
        if (habbo.RpHealthRegen.Running)
            habbo.RpHealthRegen.Stop();
        habbo.SaveRpStats();

        // Wrapped in "*" so the client narrates it: "*Twist commits suicide*".
        room.SendPacket(new ChatComposer(user.VirtualId, "*commits suicide*", 0, ActionBubble));
        PoliceState.SendStats(room, user, habbo);
        room.GetRoomUserManager().ApplyRpKnockout(user);
    }
}
