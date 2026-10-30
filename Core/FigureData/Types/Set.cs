namespace Plus.Core.FigureData.Types;

internal class Set
{
    public Set(int id, string gender, int clubLevel, bool colorable, bool selectable, bool preselectable)
    {
        Id = id;
        Gender = gender;
        ClubLevel = clubLevel;
        Colorable = colorable;
        Selectable = selectable;
        Preselectable = preselectable;
        Parts = new();
    }

    public int Id { get; set; }
    public string Gender { get; set; }
    public int ClubLevel { get; set; }
    public bool Colorable { get; set; }
    public bool Selectable { get; set; }
    public bool Preselectable { get; set; }
    public Dictionary<string, Part> Parts { get; set; }

    // pixelrp: colors are kept when any part has a color layer, as the client
    // and the renderer draw them - some imported sets are colorable="0" over
    // parts that take a color, and saving them lost the color.
    public bool TakesColor => Colorable || Parts.Values.Any(x => x.ColorIndex > 0);
}