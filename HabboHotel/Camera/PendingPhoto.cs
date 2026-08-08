namespace Plus.HabboHotel.Camera;

public record PendingPhoto(string PhotoId, uint RoomId, string Url, long TakenUnixMs);
