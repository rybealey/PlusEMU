using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>
/// pixelrp City Panel: save a uniform ("" removes it). Everyone wearing it now
/// is re-dressed at once (UniformManager.Save). Logged like a staff command.
/// </summary>
internal class RpCityUniformSaveEvent : IPacketEvent
{
    private readonly ICommandManager _commandManager;

    public RpCityUniformSaveEvent(ICommandManager commandManager) => _commandManager = commandManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var kind = packet.ReadString() == UniformManager.KindPrisoner ? UniformManager.KindPrisoner : UniformManager.KindRank;
        var rankId = packet.ReadInt();
        var gender = packet.ReadString() == "F" ? "F" : "M";
        var figure = packet.ReadString() ?? "";
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.Can(habbo, CityPanelAccess.Capability.Uniforms))
            return Task.CompletedTask;
        if (kind == UniformManager.KindRank && rankId <= 0)
            return Task.CompletedTask;

        UniformManager.Save(kind, rankId, gender, figure, habbo!.Id);
        _commandManager.LogCommand(habbo.Id, $"city-panel uniform {kind} rank={rankId} gender={gender} figure={UniformManager.Sanitise(figure)}", habbo.MachineId);

        var saved = UniformManager.Get(kind, rankId, gender);
        session.Send(new RpCityUniformComposer(kind, rankId, gender, saved, saved.Length == 0 ? "Uniform removed." : "Uniform saved."));
        session.Send(new RpCityUniformsComposer(CityUniforms.Corporations(), CityUniforms.Prisoner()));
        return Task.CompletedTask;
    }
}
