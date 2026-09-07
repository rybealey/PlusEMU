namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>pixelrp: kiss someone standing next to you. See <see cref="SocialCommand"/>.</summary>
internal class KissCommand : SocialCommand
{
    public override string Key => "kiss";

    public override string Description => "Kiss another user.";

    protected override string SelfMessage => "You cannot kiss yourself.";

    protected override string Action(string targetName) => $"leans in and gives {targetName} a kiss on the lips";
}
