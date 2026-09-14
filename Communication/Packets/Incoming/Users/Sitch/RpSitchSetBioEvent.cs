using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>pixelrp: the line under your name on Sitch. Filtered like a post - other people read it.</summary>
internal class RpSitchSetBioEvent : IPacketEvent
{
    private const int MaxBio = 160;

    private readonly IWordFilterManager _wordFilterManager;

    public RpSitchSetBioEvent(IWordFilterManager wordFilterManager) => _wordFilterManager = wordFilterManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var bio = _wordFilterManager.CheckMessage(packet.ReadString() ?? "").Trim();
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;

        if (bio.Length > MaxBio) bio = bio.Substring(0, MaxBio);
        SitchUtility.SetBio(habbo.Id, bio);
        SitchUtility.SendProfile(session, habbo.Id);
        return Task.CompletedTask;
    }
}
