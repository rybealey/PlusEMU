using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class SetMannequinFigureEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null || !room.CheckRights(session, true))
            return Task.CompletedTask;
        var itemId = packet.ReadUInt();
        var item = session.GetHabbo().CurrentRoom.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return Task.CompletedTask;
        var gender = session.GetHabbo().Gender.ToLower();
        var figure = session.GetHabbo().Look.Split('.').Where(str => !str.Contains("hr") && !str.Contains("hd") && !str.Contains("he") && !str.Contains("ea") && !str.Contains("ha"))
            .Aggregate("", (current, str) => $"{current}{str}.");
        figure = figure.TrimEnd('.');
        // Set rather than assigning into Data: the setter is what marks the
        // item dirty, and the periodic save reads ExtraData.Serialize().
        var data = ItemBehaviourUtility.MannequinData(item);
        data.Set("GENDER", gender);
        data.Set("FIGURE", figure);
        if (!data.Data.ContainsKey("OUTFIT_NAME"))
            data.Set("OUTFIT_NAME", "Default");
        item.UpdateState(true, true);
        return Task.CompletedTask;
    }
}