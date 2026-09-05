using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: toggle passive status from chat.
///
/// A chat front door onto two flows that already exist, and it deliberately
/// behaves identically to both:
///   - activating matches clicking the smoothie in the Backpack
///     (RpUseItemEvent, case "smoothie"), safe-zone and full-health gates
///     included, so the command is not a way around them;
///   - deactivating matches the x on the HUD passive tag
///     (RpPassiveCancelEvent), which discards the remaining time without
///     refunding the smoothie.
///
/// Already being passive takes priority: :passive while passive turns it OFF
/// and never looks for a second smoothie, so it cannot be spent by accident.
/// </summary>
internal class PassiveCommand : IChatCommand
{
    public string Key => "passive";
    public string PermissionRequired => "command_passive";

    public string Parameters => "";

    public string Description => "Drink a Passive Smoothie, or end your passive status.";

    /// <summary>The backpack item id the smoothie is stored under.</summary>
    private const string SmoothieItem = "smoothie";

    /// <summary>How long one smoothie lasts. Matches RpUseItemEvent.</summary>
    private const int PassiveSeconds = 3600;

    /// <summary>
    /// Yellow bubble, the style a consumed backpack item announces itself with.
    /// Wrapped in asterisks so the client renders it as an action - bold, with
    /// the opening marker moved in front of the actor's username.
    /// </summary>
    private const int ConsumeBubble = 5;

    /// <summary>
    /// Ending passive is announced like the other actions: the blue action
    /// bubble the fight commands use (PushCommand.FightBubble), so the
    /// asterisk lands before the name and there is no colon. Kept in step
    /// with RpPassiveCancelEvent (the HUD's cancel).
    /// </summary>
    private const int CancelBubble = 4;

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;

        habbo.EnsureRpStatsLoaded();

        var roomUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (roomUser == null)
            return;

        // Already passive: this is the toggle off, and it must not consider a
        // smoothie at all.
        if (habbo.RpPassiveSeconds > 0)
        {
            habbo.RpPassiveSeconds = 0;
            habbo.RpPassiveLastTick = 0;
            habbo.SaveRpStats();

            roomUser.OnChat(CancelBubble, "*discovers newfound anger, eliminating their passive state*", true);
            room.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 0, habbo.Rank >= 5 ? 1 : 0));

            // Drop the passive enable if it (and only it) is showing.
            if (habbo.Effects != null && habbo.Effects.CurrentEffect == Habbo.PassiveEnableEffectId)
                habbo.Effects.ApplyEffect(0);
            return;
        }

        // Lowest slot holding a smoothie. Slot order is what the player sees in
        // the Backpack, so spending the first one is the least surprising.
        var slot = habbo.LoadRpInventory()
            .Where(entry => (entry.Item == SmoothieItem))
            .Select(entry => entry.Slot)
            .DefaultIfEmpty(0)
            .Min();

        if (slot == 0)
        {
            session.SendWhisper("There's no Passive Smoothie in your backpack. Pick one up at The Muse.");
            return;
        }

        // The same two gates the Backpack path applies, checked BEFORE the
        // consume so a failed one never burns the smoothie.
        if (!room.IsSafeZone)
        {
            session.SendWhisper("You can only drink a Passive Smoothie in a safe zone.");
            return;
        }

        if (habbo.RpHealth < habbo.RpHealthMax)
        {
            session.SendWhisper("You need full health to drink a Passive Smoothie.");
            return;
        }

        habbo.ConsumeRpItem(slot);
        habbo.RpPassiveSeconds = PassiveSeconds;
        habbo.RpPassiveLastTick = 0;
        habbo.SaveRpStats();

        roomUser.OnChat(ConsumeBubble, "*consumes the Kylie Jeener smoothie, activating passive status*", true);
        room.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 1, habbo.Rank >= 5 ? 1 : 0));

        // Wear the passive enable immediately on activation.
        if (habbo.Effects != null)
            habbo.Effects.ApplyEffect(Habbo.PassiveEnableEffectId);

        // The smoothie is gone from a slot, so the open Backpack must be told.
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
    }
}
