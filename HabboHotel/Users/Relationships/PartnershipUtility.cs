using Dapper;
using MySqlConnector;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Messenger;

namespace Plus.HabboHotel.Users.Relationships;

/// <summary>
/// pixelrp: formal partnerships - made with :propose, ended with :divorce, and
/// the ONLY relationship a player's profile shows.
///
/// Relationships used to be self-declared: anyone could pin a heart, a smile or
/// a skull on anyone from the avatar menu. They are earned now. The menu no
/// longer offers it, SetRelationshipEvent swallows the packet, and the infostand's
/// rows are answered from here - a partner is the Love row and nothing else is
/// ever listed.
///
/// NOT TIED TO THE FRIENDS LIST. The stock relationship lives on a
/// messenger_friendships row, which would make unfriending a quiet divorce and a
/// stranger impossible to marry. A partnership is its own table instead.
///
/// TWO ROWS PER COUPLE, (a,b) and (b,a), so "who is X's partner" is one
/// primary-key read from either side. The primary key on user_id is also what
/// makes a second partnership impossible: both rows go in with ONE statement,
/// which InnoDB applies whole or not at all, so two accepts racing for the same
/// person end with one partnership and one duplicate key - never a triangle.
/// </summary>
public static class PartnershipUtility
{
    /// <summary>Relationship type 1 - the heart. See MessengerFriend on the client.</summary>
    public const int LoveRelationship = 1;

    /// <summary>This player's partner's id, or 0 when they have none.</summary>
    public static int PartnerOf(int userId)
    {
        if (userId <= 0)
            return 0;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int?>(
            "SELECT `partner_id` FROM `rp_partnerships` WHERE `user_id` = @userId LIMIT 1",
            new { userId }) ?? 0;
    }

    /// <summary>
    /// Partner these two, or false when either already is. The duplicate key is
    /// the answer rather than an error: it is the database refusing exactly the
    /// thing the callers' own checks exist to refuse, and it is the one that
    /// cannot lose a race.
    /// </summary>
    public static bool TryPartner(int a, int b)
    {
        if (a <= 0 || b <= 0 || a == b)
            return false;
        var since = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute(
                "INSERT INTO `rp_partnerships` (`user_id`,`partner_id`,`since`) VALUES (@a, @b, @since), (@b, @a, @since)",
                new { a, b, since });
            return true;
        }
        catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return false;
        }
    }

    /// <summary>
    /// End this player's partnership, from their side alone. Returns the ex's id,
    /// or 0 when there was nothing to end. Both rows go, keyed on both ids, so a
    /// half-written couple is cleaned up by either of them.
    /// </summary>
    public static int Divorce(int userId)
    {
        var partnerId = PartnerOf(userId);
        if (partnerId == 0)
            return 0;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "DELETE FROM `rp_partnerships` WHERE `user_id` IN (@userId, @partnerId)",
            new { userId, partnerId });
        return partnerId;
    }

    /// <summary>A player's name whether or not they are online - for an ex who is not.</summary>
    public static string NameOf(int userId)
    {
        if (userId <= 0)
            return string.Empty;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<string>(
            "SELECT `username` FROM `users` WHERE `id` = @userId LIMIT 1",
            new { userId }) ?? string.Empty;
    }

    /// <summary>
    /// The infostand's rows for this player, in the shape GetRelationshipsComposer
    /// already takes: their partner as the single Love row, or nothing at all.
    /// </summary>
    public static Dictionary<int, (MessengerBuddy buddy, int count)> RelationshipsFor(int userId)
    {
        var relationships = new Dictionary<int, (MessengerBuddy buddy, int count)>();
        var partnerId = PartnerOf(userId);
        if (partnerId == 0)
            return relationships;

        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var partner = connection.QuerySingleOrDefault<(string Username, string Look)?>(
            "SELECT `username`, `look` FROM `users` WHERE `id` = @partnerId LIMIT 1",
            new { partnerId });
        if (partner == null)
            return relationships;

        relationships[LoveRelationship] = (new MessengerBuddy
        {
            Id = partnerId,
            Username = partner.Value.Username ?? string.Empty,
            Look = partner.Value.Look ?? string.Empty
        }, 1);
        return relationships;
    }

    /// <summary>
    /// Tell a room this player's rows changed. The infostand filters the packet by
    /// user id, so anyone with that player's card open sees the heart arrive or
    /// go without reopening it, and everyone else ignores it.
    /// </summary>
    public static void PushRelationships(Room? room, int userId)
    {
        if (room == null || userId <= 0)
            return;
        room.SendPacket(new GetRelationshipsComposer(userId, RelationshipsFor(userId)));
    }
}
