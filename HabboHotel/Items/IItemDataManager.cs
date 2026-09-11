namespace Plus.HabboHotel.Items;

public interface IItemDataManager
{
    void Init();
    ItemDefinition GetItemByName(string name);
    Dictionary<int, uint> Gifts { get; } //<SpriteId, Item>
    Dictionary<uint, ItemDefinition> Items { get; }

    /// <summary>
    /// Definitions the Function Tool has edited, so a room can re-send their
    /// current record to anyone entering it.
    ///
    /// The client keeps its own copy of a furni's name and walkability in
    /// FurnitureData, loaded from gamedata on disk. A live edit patches every
    /// connected client, but the next login reads that file again and gets the
    /// old values back - which is invisible for walkability, where the server
    /// decides, and very visible for a name.
    /// </summary>
    HashSet<uint> EditedDefinitions { get; }
}