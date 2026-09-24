using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Rooms.Chat;

/// <summary>
/// pixelrp macros: one key press that runs several lines, in order. The client
/// sends this only when a key is bound to more than one command - a key with a
/// single command still goes out as ordinary chat, exactly as before.
///
/// Every line takes the same path a typed line of chat takes
/// (ChatEvent.ProcessAsync), so commands, mutes, the chatlog and the word filter
/// behave identically. The one difference is flood control: the press counts
/// ONCE for its commands, because one key running three commands is one action,
/// and counting each would mute a player for pressing their own macro twice. A
/// plain line of chat in the batch still counts like any other line of chat, so
/// a macro can never say more than typing could.
///
/// Processing stops at the first line that must end it (flood-muted, muted,
/// banned), and whenever the player is no longer in the room the press started
/// in - a command can move them, and the rest must not run against the room
/// they left.
/// </summary>
public class RpFireMacroEvent : IPacketEvent
{
    /// <summary>Mirrored by the client as MACRO_MAX_PER_KEY.</summary>
    public const int MaxLinesPerPress = 5;

    /// <summary>The same cap ChatEvent applies to a typed line.</summary>
    private const int MaxLineLength = 100;

    private readonly ChatEvent _chat;

    public RpFireMacroEvent(ChatEvent chat)
    {
        _chat = chat;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return;

        var colour = packet.ReadInt();
        var count = packet.ReadInt();
        if (count < 1 || count > MaxLinesPerPress)
            return;

        var lines = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var line = StringCharFilter.Escape(packet.ReadString());
            if (line.Length > MaxLineLength)
                line = line.Substring(0, MaxLineLength);
            if (line.Length > 0)
                lines.Add(line);
        }

        var commandCounted = false;
        foreach (var line in lines)
        {
            if (session.GetHabbo() == null || session.GetHabbo().CurrentRoom != room)
                return;
            var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
            if (user == null)
                return;

            // Commands share one flood count per press; plain chat counts per line.
            var isCommand = line.StartsWith(":", StringComparison.CurrentCulture);
            var countFlood = !isCommand || !commandCounted;
            if (isCommand)
                commandCounted = true;

            if (!await _chat.ProcessAsync(session, room, user, line, colour, countFlood))
                return;
        }
    }
}
