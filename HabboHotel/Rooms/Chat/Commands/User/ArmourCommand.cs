using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :armour - hide or show the Kevlar vest your body armour puts on
/// your avatar (Twist, 2026-10-29). Staff or VIP (325). Only the look: hidden
/// armour is still worn and still soaks its share of every hit. Kept on
/// users.armour_hidden, so it stays the way it was left after logging out,
/// and it can be set with no armour on for when some goes on.
/// :armor is the same command (ArmorCommand).
/// </summary>
internal class ArmourCommand : IChatCommand
{
    public const string Permission = "command_armour";

    public virtual string Key => "armour";

    public string PermissionRequired => Permission;

    public string Parameters => "";

    public string Description => "Hide or show your armour's vest.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        habbo.ArmourHidden = !habbo.ArmourHidden;
        habbo.SaveKey("armour_hidden", habbo.ArmourHidden ? "1" : "0");
        UniformManager.Refresh(session);
        session!.SendWhisper(habbo.ArmourHidden ? "Your armour is hidden." : "Your armour is visible.");
    }
}

/// <summary>:armor - the same as :armour.</summary>
internal class ArmorCommand : ArmourCommand
{
    public override string Key => "armor";
}
