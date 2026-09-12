namespace Plus.HabboHotel.Rooms.AI;

public enum BotAiType
{
    Pet,
    Generic,
    Bartender,

    // pixelrp: the bank teller. A bot bought from the Bank Teller preset
    // carries this through BotUtility.GetAiFromString, which is the only thing
    // that survives the purchase - `bots` keeps no link back to the catalog
    // row it came from, so the AI type IS the identity.
    Banker
}