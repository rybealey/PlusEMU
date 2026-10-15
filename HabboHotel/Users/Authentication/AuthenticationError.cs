namespace Plus.HabboHotel.Users.Authentication;

public enum AuthenticationError
{
    EmptySSO,
    InvalidSSO,
    NoAccountFound,
    LoginProhibited,
    // pixelrp: the connection closed while the login was still running.
    ConnectionClosed
}