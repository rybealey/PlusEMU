using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Permissions;

public interface IPermissionManager
{
    void Init();
    bool TryGetGroup(int id, out PermissionGroup group);
    List<string> GetPermissionsForPlayer(Habbo player);
    List<string> GetCommandsForPlayer(Habbo player);

    /// <summary>
    /// pixelrp: give one player a command their rank does not, for good.
    /// Idempotent. Takes effect when their permissions are next rebuilt.
    /// </summary>
    void UnlockCommand(int userId, string command);
}