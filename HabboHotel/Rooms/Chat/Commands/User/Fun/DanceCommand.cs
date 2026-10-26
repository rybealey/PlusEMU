using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;

internal class DanceCommand : IChatCommand
{
    public string Key => "dance";
    public string PermissionRequired => "command_dance";

    public string Parameters => "%DanceId%";

    public string Description => "Too lazy to dance the proper way? Do it like this!";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var thisUser = session.GetHabbo().CurrentRoom.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (thisUser == null)
            return;
        if (parameters.Length == 0)
        {
            session.SendWhisper("Please enter an ID of a dance.");
            return;
        }
        if (int.TryParse(parameters[0], out var danceId))
        {
            if (danceId > 4 || danceId < 0)
            {
                session.SendWhisper("The dance ID must be between 0 and 4!");
                return;
            }
            // pixelrp: kept as well as shown, so whoever comes in later sees it
            // too - and the enable hidden while dancing and put back after, as
            // the dance menu does (DanceEvent).
            if (danceId == 0)
            {
                thisUser.StopDancing();
                return;
            }
            if (session.GetHabbo().Effects.CurrentEffect > 0)
                room.SendPacket(new AvatarEffectComposer(thisUser.VirtualId, 0));
            thisUser.DanceId = danceId;
            room.SendPacket(new DanceComposer(thisUser, danceId));
        }
        else
            session.SendWhisper("Please enter a valid dance ID.");
    }
}