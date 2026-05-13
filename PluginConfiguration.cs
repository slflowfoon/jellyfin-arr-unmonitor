using MediaBrowser.Model.Plugins;

namespace ArrUnmonitor;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = true;

    public bool DryRun { get; set; }

    public bool ProcessMovies { get; set; } = true;

    public bool ProcessSeries { get; set; } = true;

    public bool RequireProviderId { get; set; } = true;

    public string RadarrUrl { get; set; } = string.Empty;

    public string RadarrApiKey { get; set; } = string.Empty;

    public string SonarrUrl { get; set; } = string.Empty;

    public string SonarrApiKey { get; set; } = string.Empty;
}
