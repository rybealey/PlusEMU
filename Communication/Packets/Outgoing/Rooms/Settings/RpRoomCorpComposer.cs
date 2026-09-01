using System.Collections.Generic;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp: a room's roleplay-corp config for Room settings > Roleplay:
/// its headquarters corporation, that corp's ranks with per-rank work
/// authorization, and the room's emergency-service access flags. Sent
/// alongside RoomSettingsDataComposer when the window opens, and echoed
/// after every RpSetRoomCorp / RpSetHqRank / RpSetEmergency write.
/// </summary>
public class RpRoomCorpComposer : IServerPacket
{
    public readonly record struct RankRow(int RankId, int RankOrder, string RankName, bool Authorized);

    private readonly int _roomId;
    private readonly int _corpId;
    private readonly IReadOnlyList<RankRow> _ranks;
    private readonly bool _allowMedical;
    private readonly bool _allowPolice;
    private readonly bool _allowStaff;

    public uint MessageId => ServerPacketHeader.RpRoomCorpComposer;

    public RpRoomCorpComposer(int roomId, int corpId, IReadOnlyList<RankRow> ranks,
        bool allowMedical, bool allowPolice, bool allowStaff)
    {
        _roomId = roomId;
        _corpId = corpId;
        _ranks = ranks ?? new List<RankRow>();
        _allowMedical = allowMedical;
        _allowPolice = allowPolice;
        _allowStaff = allowStaff;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteInteger(_corpId);
        packet.WriteInteger(_ranks.Count);
        foreach (var rank in _ranks)
        {
            packet.WriteInteger(rank.RankId);
            packet.WriteInteger(rank.RankOrder);
            packet.WriteString(rank.RankName);
            packet.WriteInteger(rank.Authorized ? 1 : 0);
        }
        packet.WriteInteger(_allowMedical ? 1 : 0);
        packet.WriteInteger(_allowPolice ? 1 : 0);
        packet.WriteInteger(_allowStaff ? 1 : 0);
    }
}
