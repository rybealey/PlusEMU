using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :floorplan - open the floor plan editor for the room the caller
/// is standing in. Staff only. The editor is a client-side view opened only
/// by a link event, so we signal the client (RpOpenFloorplanComposer) to open
/// it; it uses the floor data it already received on room entry.
/// </summary>
internal class FloorplanCommand : IChatCommand
{
    public string Key => "floorplan";
    public string PermissionRequired => "command_floorplan";

    public string Parameters => "";

    public string Description => "Open the floor plan editor for the current room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (room == null)
            return;
        session.Send(new RpOpenFloorplanComposer());
    }
}
