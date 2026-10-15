using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Camera;

/// <summary>
/// pixelrp: lets go of a player's pending camera photo when they log out.
///
/// A photo that has been taken but not yet bought or published is held per
/// player (CameraPhotoManager), along with the thumbnail rendered for it, and
/// nothing ever removed either - every player who had ever used the camera
/// kept their last picture in memory until the next restart. The buy and
/// publish buttons belong to the camera window of the session that took it,
/// which is gone with the logout.
/// </summary>
internal class CameraLogoutTask : IAuthenticationTask
{
    private readonly ICameraPhotoManager _cameraPhotoManager;

    public CameraLogoutTask(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task UserLoggedOut(Habbo habbo)
    {
        _cameraPhotoManager.Forget(habbo.Id);
        return Task.CompletedTask;
    }
}
