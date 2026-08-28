using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

public class CatalogIndexComposer : IServerPacket
{
    private readonly GameClient _session;
    private readonly ICollection<CatalogPage> _pages;

    public uint MessageId => ServerPacketHeader.CatalogIndexComposer;

    public CatalogIndexComposer(GameClient session, ICollection<CatalogPage> pages)
    {
        _session = session;
        _pages = pages;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // pixelrp: the index wire format is recursive (every node declares
        // its child count, children follow) - the old iterative walk capped
        // the tree at three levels below root and wrote a hardcoded 0 child
        // count at the last one, which both hid deeper pages (e.g. the shop's
        // Seasonal > Holiday > Year tree) and desynced the declared counts.
        // Walk the whole tree instead.
        WriteRootIndex(packet);
        foreach (var parent in _pages)
        {
            if (parent.ParentId != -1 || !CanSee(parent))
                continue;
            WriteBranch(packet, parent);
        }
        packet.WriteBoolean(false);
        packet.WriteString("NORMAL");
    }

    private bool CanSee(CatalogPage page) =>
        !(page.MinimumRank > _session.GetHabbo().Rank || page.MinimumVip > _session.GetHabbo().VipRank && _session.GetHabbo().Rank == 1);

    private void WriteBranch(IOutgoingPacket packet, CatalogPage page)
    {
        // CalcTreeSize and this loop apply the same CanSee filter, so the
        // declared child count always matches the children actually written.
        if (page.Enabled)
            WritePage(packet, page, CalcTreeSize(_pages, page.Id));
        else
            WriteNodeIndex(packet, page, CalcTreeSize(_pages, page.Id));
        foreach (var child in _pages)
        {
            if (child.ParentId != page.Id || !CanSee(child))
                continue;
            WriteBranch(packet, child);
        }
    }

    public void WriteRootIndex(IOutgoingPacket packet)
    {
        packet.WriteBoolean(true);
        packet.WriteInteger(0);
        packet.WriteInteger(-1);
        packet.WriteString("root");
        packet.WriteString(string.Empty);
        packet.WriteInteger(0);
        packet.WriteInteger(CalcTreeSize(_pages, -1));
    }

    public void WriteNodeIndex(IOutgoingPacket packet, CatalogPage page, int treeSize)
    {
        packet.WriteBoolean(page.Visible);
        packet.WriteInteger(page.Icon);
        packet.WriteInteger(-1);
        packet.WriteString(page.Link);
        packet.WriteString(page.Caption);
        packet.WriteInteger(0);
        packet.WriteInteger(treeSize);
    }

    public void WritePage(IOutgoingPacket packet, CatalogPage page, int treeSize)
    {
        packet.WriteBoolean(page.Visible);
        packet.WriteInteger(page.Icon);
        packet.WriteInteger(page.Id);
        packet.WriteString(page.Link);
        packet.WriteString(page.Caption);
        packet.WriteInteger(page.ItemOffers.Count);
        foreach (var i in page.ItemOffers.Keys) packet.WriteInteger(i);
        packet.WriteInteger(treeSize);
    }

    public int CalcTreeSize(ICollection<CatalogPage> pages, int parentId)
    {
        var i = 0;
        foreach (var page in pages)
        {
            if (page.MinimumRank > _session.GetHabbo().Rank || page.MinimumVip > _session.GetHabbo().VipRank && _session.GetHabbo().Rank == 1 || page.ParentId != parentId)
                continue;
            if (page.ParentId == parentId)
                i++;
        }
        return i;
    }
}