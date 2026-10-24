using Plus.HabboHotel.Corporations;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Core.FigureData;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;

internal class FacelessCommand : IChatCommand
{
    private readonly IFigureDataManager _figureDataManager;
    private readonly IDatabase _database;
    public string Key => "faceless";
    public string PermissionRequired => "command_faceless";

    public string Parameters => "";

    public string Description => "Allows you to go faceless!";

    public FacelessCommand(IFigureDataManager figureDataManager, IDatabase database)
    {
        _figureDataManager = figureDataManager;
        _database = database;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null || user.GetClient() == null)
            return;
        // pixelrp uniforms: the worn look is not theirs to save.
        if (UniformManager.RefuseChange(session))
            return;
        string[] headParts;
        // pixelrp body armour: their own look, not the vest over it
        var look = UniformManager.OwnLookOf(session.GetHabbo());
        var figureParts = look.Split('.');
        foreach (var part in figureParts)
        {
            if (part.StartsWith("hd"))
            {
                headParts = part.Split('-');
                if (!headParts[1].Equals("99999"))
                    headParts[1] = "99999";
                else
                    return;
                look = look.Replace(part, $"hd-{headParts[1]}-{headParts[2]}");
                break;
            }
        }
        // pixelrp: staff keep club clothing access even without an active VIP subscription.
        // pixelrp: HC/club clothing is not VIP-gated.
        look = _figureDataManager.ProcessFigure(look, session.GetHabbo().Gender, session.GetHabbo().Clothing.GetClothingParts, true, session.GetHabbo().WearsAnyClothing);
        UniformManager.SetOwnLook(session.GetHabbo(), look);
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.RunQuery($"UPDATE `users` SET `look` = '{look}' WHERE `id` = '{session.GetHabbo().Id}' LIMIT 1");
        }
        session.Send(new UserChangeComposer(user, true));
        session.GetHabbo().CurrentRoom.SendPacket(new UserChangeComposer(user, false));
    }
}