using Plus.Communication.Packets.Outgoing.Rooms.Avatar;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>pixelrp: kiss someone standing next to you. See <see cref="SocialCommand"/>.</summary>
internal class KissCommand : SocialCommand
{
    /// <summary>
    /// The client's kiss hearts: Habbo's Love effect (fx 9) floated over an
    /// avatar for three seconds. Sent as an expression, but the client never
    /// treats it as one - it is drawn beside whatever effect is worn
    /// (LoveHeartAddition in the renderer patch kiss-hearts), so handcuffs,
    /// the stun's birds, the ambulance and the staff and passive markers all
    /// stay on screen, and nothing here has to take an effect off or put it back.
    /// </summary>
    private const int KissHeartsExpression = 101;

    public override string Key => "kiss";

    public override string Description => "Kiss another user.";

    protected override string SelfMessage => "You cannot kiss yourself.";

    protected override string Action(string targetName) => $"leans in and gives {targetName} a kiss on the lips";

    protected override void OnLanded(Room room, RoomUser actor, RoomUser target)
    {
        room.SendPacket(new ActionComposer(actor.VirtualId, KissHeartsExpression));
        room.SendPacket(new ActionComposer(target.VirtualId, KissHeartsExpression));
    }
}
