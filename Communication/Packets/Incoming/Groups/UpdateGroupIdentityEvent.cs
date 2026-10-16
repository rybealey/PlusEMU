using Plus.Communication.Packets.Outgoing.Groups;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Dapper;

namespace Plus.Communication.Packets.Incoming.Groups;

internal class UpdateGroupIdentityEvent : IPacketEvent
{
    private readonly IGroupManager _groupManager;
    private readonly IWordFilterManager _wordFilterManager;
    private readonly IDatabase _database;

    public UpdateGroupIdentityEvent(IGroupManager groupManager, IWordFilterManager wordFilterManager, IDatabase database)
    {
        _groupManager = groupManager;
        _wordFilterManager = wordFilterManager;
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var groupId = packet.ReadInt();
        var name = _wordFilterManager.CheckMessage(packet.ReadString());
        var description = _wordFilterManager.CheckMessage(packet.ReadString());
        if (!_groupManager.TryGetGroup(groupId, out var group))
            return Task.CompletedTask;
        // pixelrp: a gang is a group flagged is_gang, but it is run only through the
        // gang window (the Rp* gang packets), which enforce roles, cost and turf. The
        // stock group packets skip all of that, so they do nothing to a gang.
        if (group.IsGang)
            return Task.CompletedTask;
        if (group.CreatorId != session.GetHabbo().Id)
            return Task.CompletedTask;
        using (var connection = _database.Connection())
        {
            connection.Execute("UPDATE `groups` SET `name`= @name, `desc` = @desc WHERE `id` = @groupId LIMIT 1",
                new { name = name, desc = description, groupId = groupId });
        }
        group.Name = name;
        group.Description = description;
        session.Send(new GroupInfoComposer(group, session));
        return Task.CompletedTask;
    }
}