namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>pixelrp: bite someone standing next to you. See <see cref="SocialCommand"/>.</summary>
internal class BiteCommand : SocialCommand
{
    public override string Key => "bite";

    public override string Description => "Bite another user.";

    protected override string SelfMessage => "You cannot bite yourself.";

    protected override string Action(string targetName) => $"gives {targetName} a little bite on their arm";

    /// <summary>The kiss's hearts - Habbo's Love (fx 9).</summary>
    protected override int OverlayExpression => 101;
}
