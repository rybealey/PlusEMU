using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP items: staff command to spawn a consumable into a player's
/// backpack. Spawn aliases map to the backpack item keys the store
/// delivers, so a spawned item redeems exactly like a purchased one.
/// </summary>
internal class SpawnCommand : ITargetChatCommand
{
    private static readonly Dictionary<string, (string ItemKey, string Name)> Items = new()
    {
        { "smoothie", ("smoothie", "Passive Smoothie") },
        { "vip31", ("vip_token_31", "VIP Token (31 days)") },
        { "vip14", ("vip_token_14", "VIP Token (14 days)") }
    };

    public string Key => "spawn";
    public string PermissionRequired => "command_spawn";

    public string Parameters => "%username% %item%";

    public string Description => "Spawn an item into a player's backpack.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var spawnKey = ((parameters.Length > 0) ? parameters[0].ToLowerInvariant() : "");
        if (!Items.TryGetValue(spawnKey, out var item))
        {
            session.SendWhisper($"Unknown item. Available: {string.Join(", ", Items.Keys)}");
            return Task.CompletedTask;
        }
        var slot = target.AddRpItem(item.ItemKey);
        if (slot == -1)
        {
            session.SendWhisper($"{target.Username}'s backpack is full.");
            return Task.CompletedTask;
        }
        target.Client?.Send(new RpInventoryComposer(target.LoadRpInventory()));
        session.SendWhisper($"Spawned a {item.Name} in {target.Username}'s backpack.");
        return Task.CompletedTask;
    }
}
