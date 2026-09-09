using Plus.Communication.Packets.Outgoing.Users;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: what region is this player in? Asked by the profile window for
/// whoever it is showing.
///
/// Read live rather than from a cache of online users: a profile is just as
/// likely to be opened on someone offline (a friend in the phone's contacts,
/// a name in the corporation directory), and the answer is one indexed lookup.
/// An unknown id answers with '' rather than nothing, so the client can tell
/// "no region" from "still waiting".
/// </summary>
internal class RpGetUserRegionEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpGetUserRegionEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (userId <= 0)
            return Task.CompletedTask;

        var region = "";
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `rp_region` FROM `users` WHERE `id` = @id LIMIT 1");
            dbClient.AddParameter("id", userId);
            var row = dbClient.GetRow();
            if (row != null)
                region = Convert.ToString(row["rp_region"]) ?? "";
        }

        session.Send(new RpUserRegionComposer(userId, region));
        return Task.CompletedTask;
    }
}
