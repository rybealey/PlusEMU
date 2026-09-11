using Plus.Communication.Packets.Outgoing.Users;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Privacy;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: what region is this player in? Asked by the profile window for
/// whoever it is showing.
///
/// Read live rather than from a cache of online users: a profile is just as
/// likely to be opened on someone offline (a friend in the phone's contacts,
/// a name in the corporation directory), and the answer is one indexed lookup.
/// An unknown id answers with '' rather than nothing, so the client can tell
/// "no region" from "still waiting". A region this viewer is not allowed
/// answers the same way: hidden and unset have to look identical from outside,
/// or the absence itself says something.
/// </summary>
internal class RpGetUserRegionEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpGetUserRegionEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var userId = packet.ReadInt();
        if (userId <= 0)
            return Task.CompletedTask;

        if (!PrivacyUtility.CanSeeRegion(habbo.Id, userId))
        {
            session.Send(new RpUserRegionComposer(userId, ""));
            return Task.CompletedTask;
        }

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
