using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp turfs: the turf panel's state (TurfManager.Describe) - whether the
/// room is a turf, who holds it and since when, the claim running in it if any
/// (held seconds, total, contested and by whom - the player and their gang), a failure to show once, and
/// the viewer's own gang so the panel can word its button.
///
/// The first four fields are the ones Room settings > Zoning reads; everything
/// after them is the panel's, appended so that reader is untouched. Sent beside
/// RpRoomZoneComposer, which still carries safe/unsafe.
/// </summary>
public class RpRoomTurfComposer : IServerPacket
{
    private readonly TurfView _view;

    public uint MessageId => ServerPacketHeader.RpRoomTurfComposer;

    public RpRoomTurfComposer(TurfView view)
    {
        _view = view;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)_view.RoomId);
        packet.WriteBoolean(_view.IsTurf);
        packet.WriteInteger(_view.OwnerGangId);
        packet.WriteString(_view.OwnerName ?? "");
        packet.WriteString(_view.OwnerColourA ?? "");
        packet.WriteString(_view.OwnerColourB ?? "");
        packet.WriteInteger(_view.HeldForSeconds);
        packet.WriteBoolean(_view.Capturing);
        packet.WriteString(_view.ClaimerName ?? "");
        packet.WriteInteger(_view.ClaimGangId);
        packet.WriteString(_view.ClaimGangName ?? "");
        packet.WriteString(_view.ClaimColourA ?? "");
        packet.WriteInteger(_view.ElapsedSeconds);
        packet.WriteInteger(_view.TotalSeconds);
        packet.WriteBoolean(_view.Contested);
        packet.WriteString(_view.ContestedBy ?? "");
        packet.WriteString(_view.FailReason ?? "");
        packet.WriteInteger(_view.ViewerGangId);
        // appended last, so a reader that stops at the viewer's gang is untouched
        packet.WriteString(_view.ContestedByGang ?? "");
    }

    /// <summary>The state of this room, as one viewer sees it.</summary>
    public static RpRoomTurfComposer For(HabboHotel.Rooms.Room room, int viewerId) =>
        new(TurfManager.Describe(room, viewerId));
}
