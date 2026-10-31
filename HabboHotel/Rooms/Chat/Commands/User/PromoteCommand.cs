using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :promote &lt;username&gt; [reason] - one step up the corporation's
/// ladder (rank, then tier), at once, for a rank with can_approve (migration
/// 334). The target must be ranked below you and stay below you. Logged by
/// itself in the Corporation Panel (CorpRanks); ranks that don't approve
/// request it in :cp instead.
/// </summary>
internal class PromoteCommand : IChatCommand
{
    public string Key => "promote";
    public string PermissionRequired => "";

    public string Parameters => "%username% [reason]";

    public string Description => "Promote someone in your corporation one step.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!parameters.Any())
        {
            session.SendWhisper("Usage: :promote <username> [reason]");
            return;
        }
        CorpRanks.Command(session, parameters[0], CorpRanks.Promotion, string.Join(" ", parameters.Skip(1)));
    }
}
