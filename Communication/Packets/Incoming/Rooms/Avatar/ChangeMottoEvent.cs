using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Avatar;

internal class ChangeMottoEvent : IPacketEvent
{
    // PixelRP: the motto is RP-managed (job/role text like "Citizen") and is
    // never player-editable. The client no longer offers the motto editor,
    // but the packet is rejected here regardless so injected packets
    // (G-Earth etc.) can't rewrite it either. The stock handler (word
    // filter, rate limiting, users.motto update, UserChangeComposer) lived
    // here — see git history if motto editing ever comes back.
    public Task Parse(GameClient session, IIncomingPacket packet) => Task.CompletedTask;
}
