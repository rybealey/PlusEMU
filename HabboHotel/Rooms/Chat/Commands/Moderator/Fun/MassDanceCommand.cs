using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

internal class MassDanceCommand : IChatCommand
{
    public string Key => "massdance";
    public string PermissionRequired => "command_massdance";

    public string Parameters => "%DanceId%";

    public string Description => "Force everyone in the room to dance to a dance of your choice.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Please enter a dance ID. (1-4)");
            return;
        }
        var danceId = Convert.ToInt32(parameters[0]);
        if (danceId < 0 || danceId > 4)
        {
            session.SendWhisper("Please enter a dance ID. (1-4)");
            return;
        }
        var users = room.GetRoomUserManager().GetRoomUsers();
        if (users.Count > 0)
        {
            foreach (var u in users.ToList())
            {
                if (u == null)
                    continue;
                // pixelrp: hands are left alone - a dance keeps what is in the
                // hand (DanceEvent), and emptying it here told nobody, so every
                // screen still showed the drink while a newcomer saw none. A
                // bot's dance is BotData's: the room shows newcomers that one.
                if (u.IsBot)
                    u.BotData.DanceId = danceId;
                else
                    u.DanceId = danceId;
                room.SendPacket(new DanceComposer(u, danceId));
            }
        }
    }
}