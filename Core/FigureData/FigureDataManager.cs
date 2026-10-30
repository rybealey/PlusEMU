using System.Xml;
using Microsoft.Extensions.Logging;
using Plus.Core.FigureData.Types;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Users.Clothing.Parts;
using Plus.Utilities;

namespace Plus.Core.FigureData;

public class FigureDataManager : IFigureDataManager
{
    // pixelrp: custom colors. A color id from 0x1000000 to 0x1FFFFFF is in no
    // palette - it carries its own RGB (id - 0x1000000), the color a player
    // picked in Change Your Outfit; the client and the imager read it the same
    // way. Hair and clothing take them; skin keeps to its palette.
    public const int CustomColorBase = 0x1000000;
    public const int CustomColorMax = 0x1FFFFFF;
    private static readonly string[] CustomColorRefused = { "hd" };

    // pixelrp: a piece takes up to three colors - a few have a third color
    // layer (sh-300363, he-3385 ...), and Change Your Outfit sets all three.
    private const int MaxColors = 3;

    private readonly ICatalogManager _catalogManager;
    private readonly ILogger<FigureDataManager> _logger;
    private readonly Dictionary<int, Palette> _palettes; //pallet id, Pallet

    private readonly List<string> _requirements;
    private readonly Dictionary<string, FigureSet> _setTypes; //type (hr, ch, etc), Set

    public FigureDataManager(ICatalogManager catalogManager, ILogger<FigureDataManager> logger)
    {
        _catalogManager = catalogManager;
        _logger = logger;
        _palettes = new();
        _setTypes = new();
        _requirements = new()
        {
            "hd",
            "ch",
            "lg"
        };
    }

    public void Init()
    {
        if (_palettes.Count > 0)
            _palettes.Clear();
        if (_setTypes.Count > 0)
            _setTypes.Clear();
        var projectSolutionPath = Directory.GetCurrentDirectory();
        var xDoc = new XmlDocument();
        xDoc.Load($"{projectSolutionPath}//Config//figuredata.xml");
        var colors = xDoc.GetElementsByTagName("colors");
        foreach (XmlNode node in colors)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                _palettes.Add(Convert.ToInt32(child.Attributes["id"].Value), new(Convert.ToInt32(child.Attributes["id"].Value)));
                foreach (XmlNode sub in child.ChildNodes)
                {
                    _palettes[Convert.ToInt32(child.Attributes["id"].Value)].Colors.Add(Convert.ToInt32(sub.Attributes["id"].Value),
                        new(Convert.ToInt32(sub.Attributes["id"].Value), Convert.ToInt32(sub.Attributes["index"].Value), Convert.ToInt32(sub.Attributes["club"].Value),
                            Convert.ToInt32(sub.Attributes["selectable"].Value) == 1, Convert.ToString(sub.InnerText)));
                }
            }
        }
        var sets = xDoc.GetElementsByTagName("sets");
        foreach (XmlNode node in sets)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                _setTypes.Add(child.Attributes["type"].Value, new(SetTypeUtility.GetSetType(child.Attributes["type"].Value), Convert.ToInt32(child.Attributes["paletteid"].Value)));
                foreach (XmlNode sub in child.ChildNodes)
                {
                    _setTypes[child.Attributes["type"].Value].Sets.Add(Convert.ToInt32(sub.Attributes["id"].Value),
                        new(Convert.ToInt32(sub.Attributes["id"].Value), Convert.ToString(sub.Attributes["gender"].Value), Convert.ToInt32(sub.Attributes["club"].Value),
                            Convert.ToInt32(sub.Attributes["colorable"].Value) == 1, Convert.ToInt32(sub.Attributes["selectable"].Value) == 1,
                            Convert.ToInt32(sub.Attributes["preselectable"].Value) == 1));
                    foreach (XmlNode subb in sub.ChildNodes)
                    {
                        if (subb.Attributes["type"] != null)
                        {
                            _setTypes[child.Attributes["type"].Value].Sets[Convert.ToInt32(sub.Attributes["id"].Value)].Parts.Add(
                                $"{Convert.ToInt32(subb.Attributes["id"].Value)}-{subb.Attributes["type"].Value}",
                                new(Convert.ToInt32(subb.Attributes["id"].Value), SetTypeUtility.GetSetType(child.Attributes["type"].Value),
                                    Convert.ToInt32(subb.Attributes["colorable"].Value) == 1, Convert.ToInt32(subb.Attributes["index"].Value), Convert.ToInt32(subb.Attributes["colorindex"].Value)));
                        }
                    }
                }
            }
        }

        //Faceless.
        _setTypes["hd"].Sets.Add(99999, new(99999, "U", 0, true, false, false));
        _logger.LogInformation("Loaded " + _palettes.Count + " Color Palettes");
        _logger.LogInformation("Loaded " + _setTypes.Count + " Set Types");
    }

    public string ProcessFigure(string figure, string gender, ICollection<ClothingParts> clothingParts, bool hasHabboClub, bool wearsAnything = false)
    {
        figure = figure.ToLower();
        gender = gender.ToUpper();
        var rebuildFigure = string.Empty;
        var figureParts = figure.Split('.');
        foreach (var part in figureParts.ToList())
        {
            var type = part.Split('-')[0];
            if (_setTypes.TryGetValue(type, out var figureSet))
            {
                var partId = Convert.ToInt32(part.Split('-')[1]);
                var colorIds = new int[MaxColors];
                if (figureSet.Sets.TryGetValue(partId, out var set))
                {
                    if (set.Gender != gender && set.Gender != "U")
                    {
                        if (figureSet.Sets.Count(x => x.Value.Gender == gender || x.Value.Gender == "U") > 0)
                        {
                            partId = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U").Value.Id;

                            //Fetch the new set.
                            figureSet.Sets.TryGetValue(partId, out set);
                            colorIds[0] = GetRandomColor(figureSet.PalletId);
                        }
                    }
                    if (set.TakesColor)
                    {
                        // One color per color layer, at most three; a part with
                        // more than that keeps none.
                        var colors = part.Split('-').Skip(2).ToArray();
                        if (colors.Length >= 1 && colors.Length <= MaxColors)
                        {
                            for (var i = 0; i < colors.Length; i++)
                                colorIds[i] = ValidColor(type, figureSet, colors[i]);
                        }
                    }
                    else
                    {
                        var ignore = new[] { "ca", "wa" };
                        if (ignore.Contains(type))
                            if (!string.IsNullOrEmpty(part.Split('-')[2]))
                                colorIds[0] = Convert.ToInt32(part.Split('-')[2]);
                    }
                    if (set.ClubLevel > 0 && !hasHabboClub)
                    {
                        partId = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U" && x.Value.ClubLevel == 0).Value.Id;
                        figureSet.Sets.TryGetValue(partId, out set);
                        colorIds[0] = GetRandomColor(figureSet.PalletId);
                    }
                    // Trailing 0s are left off, the first color never is.
                    var colorCount = MaxColors;
                    while (colorCount > 1 && colorIds[colorCount - 1] == 0)
                        colorCount--;
                    rebuildFigure = $"{rebuildFigure}{type}-{partId}-{string.Join('-', colorIds.Take(colorCount))}.";
                }
            }
        }
        foreach (var requirement in _requirements)
        {
            if (!rebuildFigure.Contains(requirement))
            {
                if (requirement == "ch" && gender == "M")
                    continue;
                if (_setTypes.TryGetValue(requirement, out var figureSet))
                {
                    var set = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U").Value;
                    if (set != null)
                    {
                        var partId = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U").Value.Id;
                        var colorId = GetRandomColor(figureSet.PalletId);
                        rebuildFigure = $"{rebuildFigure}{requirement}-{partId}-{colorId}.";
                    }
                }
            }
        }
        // pixelrp: every staff rank wears anything without buying it - every
        // Clothing Store piece and every face (Habbo.WearsAnyClothing,
        // 2026-10-22). Everyone else still needs to own a purchasable set.
        if (clothingParts != null && !wearsAnything)
        {
            var purchasableParts = _catalogManager.ClothingManager.GetClothingAllParts;
            figureParts = rebuildFigure.TrimEnd('.').Split('.');
            foreach (var part in figureParts.ToList())
            {
                var partId = Convert.ToInt32(part.Split('-')[1]);
                if (purchasableParts.Count(x => x.PartIds.Contains(partId)) > 0)
                {
                    if (clothingParts.Count(x => x.PartId == partId) == 0)
                    {
                        var type = part.Split('-')[0];
                        if (_setTypes.TryGetValue(type, out var figureSet))
                        {
                            var set = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U").Value;
                            if (set != null)
                            {
                                partId = figureSet.Sets.FirstOrDefault(x => x.Value.Gender == gender || x.Value.Gender == "U").Value.Id;
                                var colorId = GetRandomColor(figureSet.PalletId);
                                rebuildFigure = $"{rebuildFigure}{type}-{partId}-{colorId}.";
                            }
                        }
                    }
                }
            }
        }
        return rebuildFigure;
    }

    // A color the piece's palette has, a custom color where those are taken, or
    // 0; any other id becomes the palette's first color.
    private int ValidColor(string type, FigureSet figureSet, string value)
    {
        if (!int.TryParse(value, out var colorId) || colorId == 0)
            return 0;
        if (AcceptsCustomColor(type, colorId))
            return colorId;
        var palette = GetPalette(colorId);
        return palette != null && palette.Id == figureSet.PalletId ? colorId : GetRandomColor(figureSet.PalletId);
    }

    private static bool AcceptsCustomColor(string type, int colorId) =>
        colorId >= CustomColorBase && colorId <= CustomColorMax && !CustomColorRefused.Contains(type);

    public Palette GetPalette(int colorId)
    {
        return _palettes.FirstOrDefault(x => x.Value.Colors.ContainsKey(colorId)).Value;
    }

    public bool TryGetPalette(int palletId, out Palette palette) => _palettes.TryGetValue(palletId, out palette);

    public int GetRandomColor(int palletId) => _palettes[palletId].Colors.FirstOrDefault().Value.Id;

    public string FilterFigure(string figure)
    {
        return StringCharFilter.IsValid(figure) ? figure : IFigureDataManager.DefaultFigure;
    }
}