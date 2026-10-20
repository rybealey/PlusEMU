using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;

namespace Plus.HabboHotel.Items.Interactor;

public class InteractorGenericSwitch : IFurniInteractor
{
    public void OnPlace(GameClient session, Item item) { }

    public void OnRemove(GameClient session, Item item) { }

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        var modes = item.Definition.Modes - 1;
        var cycle = ClickCycle(item);
        if (session == null || !hasRights || (modes <= 0 && cycle == null)) return;
        PlusEnvironment.Game.QuestManager.ProgressUserQuest(session, QuestType.FurniSwitch);
        var currentMode = 0;
        var newMode = 0;
        if (!int.TryParse(item.LegacyDataString, out currentMode)) { }
        if (cycle != null)
            newMode = cycle[(cycle.IndexOf(currentMode) + 1) % cycle.Count];
        else if (currentMode <= 0)
            newMode = 1;
        else if (currentMode >= modes)
            newMode = 0;
        else
            newMode = currentMode + 1;
        item.LegacyDataString = newMode.ToString();
        item.UpdateState();
    }

    public void OnWiredTrigger(Item item)
    {
        var modes = item.Definition.Modes - 1;
        var cycle = ClickCycle(item);
        if (modes == 0 && cycle == null) return;
        var currentMode = 0;
        var newMode = 0;
        if (string.IsNullOrEmpty(item.LegacyDataString))
            item.LegacyDataString = "0";
        if (!int.TryParse(item.LegacyDataString, out currentMode)) return;
        if (cycle != null)
            newMode = cycle[(cycle.IndexOf(currentMode) + 1) % cycle.Count];
        else if (currentMode <= 0)
            newMode = 1;
        else if (currentMode >= modes)
            newMode = 0;
        else
            newMode = currentMode + 1;
        item.LegacyDataString = newMode.ToString();
        item.UpdateState();
    }

    /// <summary>
    /// pixelrp: the furni's own click states (ItemDefinition.ClickStates), or
    /// null to cycle 0 .. Modes - 1 as usual. Only states the count allows are
    /// kept, so lowering the count in the Function tool still takes effect, and
    /// fewer than two leaves nothing to switch between. A piece standing in a
    /// state off the list (set before the list existed) goes to the first.
    /// </summary>
    private static List<int> ClickCycle(Item item)
    {
        var states = item.Definition.ClickStates?.Where(state => state < item.Definition.Modes).ToList();
        return states is { Count: > 1 } ? states : null;
    }
}