namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>pixelrp: hug someone standing next to you. See <see cref="SocialCommand"/>.</summary>
internal class HugCommand : SocialCommand
{
    public override string Key => "hug";

    public override string Description => "Hug another user.";

    protected override string SelfMessage => "You cannot hug yourself.";

    protected override string Action(string targetName) => $"wraps their arms around {targetName}, giving them a big hug";
}
