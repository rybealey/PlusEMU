using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
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

    private void WriteBranch(IOutgoingPacket packet, CatalogPage page, bool inMirror = false)
    {
        // pixelrp: a mirror page is written as the branch it shows - that
        // page's id, link and offers, and its children - under the mirror's own
        // caption and icon. The client gets the same page ids in two places, so
        // opening either loads the one real page, and nothing is copied. A
        // mirror inside a mirrored branch is left out, so two can never chase
        // each other round. Children() only lets through a mirror whose page
        // exists and can be seen.
        var shown = page;
        if (CatalogLookup.IsMirror(page) && MirrorSource(page) is { } source)
        {
            shown = source;
            inMirror = true;
        }

        // One list for the count and the loop, so the declared child count
        // always matches the children actually written.
        var children = Children(shown.Id, inMirror);
        if (shown.Enabled)
            WritePage(packet, shown, children.Count, page);
        else
            WriteNodeIndex(packet, shown, children.Count, page);
        foreach (var child in children)
            WriteBranch(packet, child, inMirror);
    }

    private List<CatalogPage> Children(int parentId, bool inMirror)
    {
        var children = new List<CatalogPage>();
        foreach (var child in _pages)
        {
            if (child.ParentId != parentId || !CanSee(child))
                continue;
            if (CatalogLookup.IsMirror(child) && (inMirror || MirrorSource(child) is not { } source || !CanSee(source)))
                continue;
            children.Add(child);
        }
        return children;
    }

    private CatalogPage? MirrorSource(CatalogPage mirror)
    {
        _byId ??= CatalogLookup.Index(_pages);
        return _byId.TryGetValue(CatalogLookup.MirrorSourceId(mirror), out var source) && !CatalogLookup.IsMirror(source)
            ? source
            : null;
    }

    private Dictionary<int, CatalogPage>? _byId;

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

    // `label` is the page whose caption and icon are shown - the page itself,
    // or the mirror standing in for it.
    public void WriteNodeIndex(IOutgoingPacket packet, CatalogPage page, int treeSize, CatalogPage? label = null)
    {
        label ??= page;
        packet.WriteBoolean(label.Visible);
        packet.WriteInteger(label.Icon);
        packet.WriteInteger(-1);
        packet.WriteString(page.Link);
        packet.WriteString(label.Caption);
        packet.WriteInteger(0);
        packet.WriteInteger(treeSize);
    }

    public void WritePage(IOutgoingPacket packet, CatalogPage page, int treeSize, CatalogPage? label = null)
    {
        label ??= page;
        packet.WriteBoolean(label.Visible);
        packet.WriteInteger(label.Icon);
        packet.WriteInteger(page.Id);
        packet.WriteString(page.Link);
        packet.WriteString(label.Caption);
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