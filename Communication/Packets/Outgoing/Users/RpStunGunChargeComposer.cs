using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: how many shots the player's stun gun has left, and how many it
/// holds (PoliceState.StunGunShots) - the green bar on the stun gun in the
/// backpack. Sent at login, after every shot, and when the police locker
/// restocks the gun. The count belongs to the player, not the item, so it is
/// sent whether or not they hold a gun right now; the client only draws it on
/// a stun gun.
/// </summary>
public class RpStunGunChargeComposer : IServerPacket
{
    private readonly int _left;
    private readonly int _max;

    public uint MessageId => ServerPacketHeader.RpStunGunChargeComposer;

    public RpStunGunChargeComposer(int left, int max)
    {
        _left = left;
        _max = max;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_left);
        packet.WriteInteger(_max);
    }
}
