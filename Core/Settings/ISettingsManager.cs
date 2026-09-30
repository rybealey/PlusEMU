namespace Plus.Core.Settings;

public interface ISettingsManager
{
    string TryGetValue(string value);
    Task Reload();

    /// <summary>pixelrp: write one setting - to server_settings and to memory, so it is read at once and survives a restart.</summary>
    void Set(string key, string value);
}