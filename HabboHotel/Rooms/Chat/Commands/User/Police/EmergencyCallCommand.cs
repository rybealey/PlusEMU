using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp: :911 &lt;message&gt; - call the police from the room you are in. Any
/// player; the call joins the queue every on-duty officer sees, and waits there
/// for the next one when nobody is on duty (EmergencyCalls). :999 is the same
/// call (EmergencyCallAliasCommand).
/// </summary>
internal class EmergencyCallCommand : IChatCommand
{
    private readonly IWordFilterManager _wordFilterManager;

    public EmergencyCallCommand(IWordFilterManager wordFilterManager) => _wordFilterManager = wordFilterManager;

    public virtual string Key => "911";

    public string PermissionRequired => "";

    public string Parameters => "%message%";

    public string Description => "Call the police: say what is happening and where.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (session?.GetHabbo() == null || room == null)
            return;
        var message = _wordFilterManager.CheckMessage(CommandManager.MergeParams(parameters));
        var reply = EmergencyCalls.Submit(session, message);
        if (reply.Length > 0)
            session.SendWhisper(reply);
    }
}

/// <summary>:999 - the same call as :911.</summary>
internal class EmergencyCallAliasCommand : EmergencyCallCommand
{
    public EmergencyCallAliasCommand(IWordFilterManager wordFilterManager) : base(wordFilterManager) { }

    public override string Key => "999";
}
