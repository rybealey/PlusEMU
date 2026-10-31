using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :loa &lt;days&gt; [reason] - ask your corporation for leave from
/// today; a rank that approves decides in the Corporation Panel. :loa end
/// comes back early, or withdraws a request. While on leave you can't clock
/// in, and the week counts as quota met (CorpRecords).
/// </summary>
internal class LoaCommand : IChatCommand
{
    public string Key => "loa";
    public string PermissionRequired => "";

    public string Parameters => "%days% [reason]";

    public string Description => "Ask your corporation for leave, or :loa end to come back.";

    public void Execute(GameClient session, Room room, string[] parameters) => CorpRecords.LoaCommand(session, parameters);
}
