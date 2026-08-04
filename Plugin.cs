using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace ArrUnmonitor;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginGuid = Guid.Parse("d8936cb9-b278-4fcf-897b-4a7dec1d9879");

    private readonly ILogger<Plugin> _logger;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        PatchWebIndex(applicationPaths.WebPath);
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Arr Unmonitor";

    public override Guid Id => PluginGuid;

    public override string Description => "Unmonitors matching Radarr, Sonarr, and Sportarr items when they are deleted from Jellyfin.";

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = "ArrUnmonitorConfigPage",
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        }
    ];

    private void PatchWebIndex(string webPath)
    {
        var indexPath = Path.Combine(webPath, "index.html");

        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("Arr Unmonitor: index.html not found at {Path}", indexPath);
            return;
        }

        var content = File.ReadAllText(indexPath);
        const string marker = "plugins/ArrUnmonitor/ClientScript";
        var version = GetType().Assembly.GetName().Version?.ToString() ?? "1";
        var tag = $"""<script src="/plugins/ArrUnmonitor/ClientScript?v={Uri.EscapeDataString(version)}" defer></script>""";

        if (content.Contains(marker, StringComparison.Ordinal))
        {
            var updated = Regex.Replace(
                content,
                """<script\b[^>]*\bsrc=["']/plugins/ArrUnmonitor/ClientScript(?:\?[^"']*)?["'][^>]*>\s*</script>""",
                tag,
                RegexOptions.IgnoreCase);

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(indexPath, updated);
                _logger.LogInformation("Arr Unmonitor: Updated Jellyfin web client script version");
                return;
            }

            _logger.LogDebug("Arr Unmonitor: index.html already patched with current script tag");
            return;
        }

        content = content.Replace("</head>", tag + "</head>", StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(indexPath, content);
        _logger.LogInformation("Arr Unmonitor: Patched Jellyfin web client");
    }
}
