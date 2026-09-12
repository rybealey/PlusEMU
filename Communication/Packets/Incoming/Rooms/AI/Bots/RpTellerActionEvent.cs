using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.AI.Types;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Bots;

/// <summary>
/// pixelrp: the player picked something from a bank teller's menu.
///
/// Its own packet rather than a ride on SaveBotActionEvent, which is owner-only
/// by construction - it rejects anyone who is not the bot's owner, and a teller
/// exists precisely to serve people who do not own it.
///
/// EVERYTHING THE CLIENT THINKS IT KNOWS IS RE-CHECKED HERE. The menu only
/// offers Open account to somebody without one, and only offers the entries at
/// all on a bot the teller push named - but both of those are affordances. This
/// confirms the avatar is real, is a bot, is a BANKER bot, and is standing in
/// the room the player is standing in; the teller itself then re-checks the
/// two-tile range and the account state before it says a word.
///
/// A bad id is silence, not a refusal. A forged virtual id has nothing to
/// answer to, and telling somebody their forgery missed is a free probe.
/// </summary>
internal class RpTellerActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;

        if (room == null)
            return Task.CompletedTask;

        var virtualId = packet.ReadInt();
        var action = packet.ReadInt();

        if (!Enum.IsDefined(typeof(TellerAction), action))
            return Task.CompletedTask;

        var bot = room.GetRoomUserManager()?.GetRoomUserByVirtualId(virtualId);

        if (bot == null || !bot.IsBot || bot.IsPet || bot.BotAi is not BankerBot teller)
            return Task.CompletedTask;

        teller.Serve(session, (TellerAction)action);
        return Task.CompletedTask;
    }
}
