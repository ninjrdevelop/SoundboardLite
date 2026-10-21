namespace SoundboardLite.Core.Models;

/// <summary>Well-known keys for the AppSettings key/value table.</summary>
public static class AppSettingsKeys
{
    public const string SelectedOutputDeviceId = "SelectedOutputDeviceId";
    public const string Theme = "Theme";
    public const string LowLatencyOutput = "Output.LowLatency";

    public const string PttDefaultKey = "Ptt.DefaultKey";
    public const string PttDefaultBeginDelayMs = "Ptt.DefaultBeginDelayMs";
    public const string PttDefaultEndDelayMs = "Ptt.DefaultEndDelayMs";

    public const string ApiEnabled = "Api.Enabled";
}
