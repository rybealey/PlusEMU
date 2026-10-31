using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :demote &lt;username&gt; [reason] - one step down the corporation's
/// ladder (tier, then rank), at once, for a rank with can_approve (migration
/// 334). The target must be ranked below you. From the lowest step there is
/// nowhere to go: that is :fire. Logged by itself in the Corporation Panel
/// (CorpRanks); ranks that don't approve request it in :cp instead.
/// </summary>
internal class DemoteCommand : IChatCommand
{
    public string Key => "demote";
    public string PermissionRequired => "";

    public string Parameters => "%username% [reason]";

    public string Description => "Demote someone in your corporation one step.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Usage: :demote <username> [reason]");
            return;
        }
        CorpRanks.Command(session, parameters[0], CorpRanks.Demotion, string.Join(" ", parameters.Skip(1)));
    }
}
