using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :floorplan - open the floor plan editor for the room the caller
/// is standing in. Staff only: Builder (2) and up, in any room (migration
/// 286; saving is Room.CanEditFloorplan). The editor is a client-side view
/// opened only by a link event, so we signal the client
/// (RpOpenFloorplanComposer) to open it; it uses the floor data it already
/// received on room entry.
/// </summary>
internal class FloorplanCommand : IChatCommand
{
    public virtual string Key => "floorplan";
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

/// <summary>
/// pixelrp: :floor, the same command as :floorplan under a shorter word
/// (Twist, 2026-10-21). Same permission row (command_floorplan).
/// </summary>
internal class FloorCommand : FloorplanCommand
{
    public override string Key => "floor";
}
