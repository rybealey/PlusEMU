using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.Core;

namespace Plus.HabboHotel.GameClients;

/// <summary>
/// pixelrp: one packet sent to many clients, composed once for each client type
/// and Revision instead of once per recipient. Everyone in a group gets the
/// same bytes (GameClient.Frame), so a broadcast to a room - which nearly
/// always holds one group - composes once. For the senders that pick their own
/// audience; Room.SendPacket uses it for the whole room.
/// </summary>
internal sealed class SharedFrame
{
    private readonly IServerPacket _packet;
    private GameClient? _first;
    private byte[]? _firstBytes;
    private Dictionary<(Type, Revision), byte[]?>? _otherGroups;

    public SharedFrame(IServerPacket packet) => _packet = packet;

    public void SendTo(GameClient? client)
    {
        if (client?.Revision == null)
            return;
        byte[]? bytes;
        if (_first == null)
        {
            _first = client;
            bytes = _firstBytes = client.Frame(_packet);
        }
        else if (client.GetType() == _first.GetType() && client.Revision == _first.Revision)
        {
            bytes = _firstBytes;
        }
        else
        {
            _otherGroups ??= new();
            var key = (client.GetType(), client.Revision);
            if (!_otherGroups.TryGetValue(key, out bytes))
                _otherGroups[key] = bytes = client.Frame(_packet);
        }
        if (bytes == null)
            return;
        try
        {
            client.SendFramed(bytes);
        }
        catch (Exception e)
        {
            // One recipient's socket must not cost the rest the packet.
            ExceptionLogger.LogException(e);
        }
    }
}
