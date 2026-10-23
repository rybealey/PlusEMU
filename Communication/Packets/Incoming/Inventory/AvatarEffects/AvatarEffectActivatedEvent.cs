using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffects;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Inventory.AvatarEffects;

internal class AvatarEffectActivatedEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var effectId = packet.ReadInt();
        // pixelrp :invisible: the Ghost enable stays on until they turn visible.
        if (session.GetHabbo().IsInvisible)
        {
            session.SendWhisper("Your invisible enable stays on while you're invisible.");
            return Task.CompletedTask;
        }
        // pixelrp: City Government on duty wears the staff enable and nothing
        // else for the shift - other enables are refused until clock-out.
        if (ShiftManager.IsStaffOnDuty(session.GetHabbo().Id) && effectId != Habbo.StaffDutyEffectId)
        {
            session.SendWhisper("Your City Government enable stays on while you're on duty.");
            return Task.CompletedTask;
        }
        var effect = session.GetHabbo().Effects.GetEffectNullable(effectId, false, true);
        if (session.GetHabbo().Effects.HasEffect(effectId, true)) return Task.CompletedTask;
        if (effect.Activate()) session.Send(new AvatarEffectActivatedComposer(effect));
        return Task.CompletedTask;
    }
}