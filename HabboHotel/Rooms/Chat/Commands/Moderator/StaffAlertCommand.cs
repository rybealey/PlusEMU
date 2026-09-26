using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :sa &lt;message&gt; - whisper a message to every online staff
/// member, wherever they are in the hotel, as "[sender]: message". Built on
/// :ga (GangAlertCommand): the same line, a different audience.
///
/// It used to be a modal popup (BroadcastMessageAlertComposer) sent to rank 2
/// and up. A popup stops play for everybody it reaches and leaves no trace
/// once dismissed; a bubble reads like a conversation and lands in its own
/// Staff tab in Chat History.
///
/// Both ends are rank 5: the permission row (migration 172) gates who can SEND,
/// and <see cref="MinimumRank"/> gates who RECEIVES, so an alert never reaches
/// somebody who could not have answered it.
/// </summary>
internal class StaffAlertCommand : IChatCommand
{
    /// <summary>
    /// Bubble 5. Unlike gang alert's private 200, 5 is a style every player
    /// can pick (migration 167 opened them all), so the client cannot sort a
    /// line into the Staff tab on the bubble alone. It does what :ca's bubble
    /// 11 needs instead: bubble 5, a whisper, one naming the recipient as the
    /// speaker, and the "[sender]:" prefix - together, only this sends that.
    /// See client api/rp-chat/StaffAlert.ts.
    /// </summary>
    private const int AlertBubble = 5;

    /// <summary>The lowest rank an alert is sent to. Matches the permission.</summary>
    private const int MinimumRank = 5;

    private readonly IGameClientManager _gameClientManager;

    public string Key => "sa";
    public string PermissionRequired => "command_staff_alert";

    public string Parameters => "%message%";

    public string Description => "Send an alert to every online staff member.";

    public StaffAlertCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var message = CommandManager.MergeParams(parameters);
        if (string.IsNullOrWhiteSpace(message))
        {
            session.SendWhisper("Usage: :sa <message>");
            return;
        }

        var line = $"[{session.GetHabbo().Username}]: {message}";
        // the alert went out - the sender's chat box keeps the prefix for the next one
        session.Send(new RpRetainChatPrefixComposer(":sa"));
        // The sender is staff too, so they get the same line back as their receipt.
        foreach (var client in _gameClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() != null && client.GetHabbo().Rank >= MinimumRank)
                client.SendWhisper(line, AlertBubble);
        }
    }
}