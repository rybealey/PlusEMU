using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP items: staff command to spawn a consumable into a player's
/// backpack. Items: smoothie (Passive Smoothie).
/// </summary>
internal class SpawnCommand : ITargetChatCommand
{
    private static readonly Dictionary<string, string> Items = new()
    {
        { "smoothie", "Passive Smoothie" }
    };

    public string Key => "spawn";
    public string PermissionRequired => "command_spawn";

    public string Parameters => "%username% %item%";

    public string Description => "Spawn an item into a player's backpack.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var itemKey = ((parameters.Length > 0) ? parameters[0].ToLowerInvariant() : "");
        if (!Items.TryGetValue(itemKey, out var itemName))
        {
            session.SendWhisper($"Unknown item. Available: {string.Join(", ", Items.Keys)}");
            return Task.CompletedTask;
        }
        var slot = target.AddRpItem(itemKey);
        if (slot == -1)
        {
            session.SendWhisper($"{target.Username}'s backpack is full.");
            return Task.CompletedTask;
        }
        target.Client?.Send(new RpInventoryComposer(target.LoadRpInventory()));
        session.SendWhisper($"Spawned a {itemName} in {target.Username}'s backpack.");
        return Task.CompletedTask;
    }
}
