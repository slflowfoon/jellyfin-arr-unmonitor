using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrUnmonitor.Api;

[ApiController]
[Route("plugins/ArrUnmonitor")]
public class ConfigController : ControllerBase
{
    [HttpGet("Config")]
    [Authorize]
    public IActionResult GetConfig()
    {
        var config = Plugin.Instance!.Configuration;
        return Ok(new ConfigResponse
        {
            Enabled = config.Enabled,
            DryRun = config.DryRun,
            ProcessMovies = config.ProcessMovies,
            ProcessSeries = config.ProcessSeries,
            RequireProviderId = config.RequireProviderId,
            RadarrUrl = config.RadarrUrl,
            HasRadarrApiKey = !string.IsNullOrWhiteSpace(config.RadarrApiKey),
            SonarrUrl = config.SonarrUrl,
            HasSonarrApiKey = !string.IsNullOrWhiteSpace(config.SonarrApiKey)
        });
    }

    [HttpPost("Config")]
    [Authorize]
    public IActionResult SaveConfig([FromBody] ConfigRequest request)
    {
        var config = Plugin.Instance!.Configuration;
        config.Enabled = request.Enabled;
        config.DryRun = request.DryRun;
        config.ProcessMovies = request.ProcessMovies;
        config.ProcessSeries = request.ProcessSeries;
        config.RequireProviderId = request.RequireProviderId;
        config.RadarrUrl = request.RadarrUrl?.Trim() ?? string.Empty;
        config.SonarrUrl = request.SonarrUrl?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(request.RadarrApiKey))
        {
            config.RadarrApiKey = request.RadarrApiKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.SonarrApiKey))
        {
            config.SonarrApiKey = request.SonarrApiKey.Trim();
        }

        Plugin.Instance!.SaveConfiguration();
        return NoContent();
    }

    public sealed class ConfigRequest
    {
        public bool Enabled { get; set; } = true;

        public bool DryRun { get; set; }

        public bool ProcessMovies { get; set; } = true;

        public bool ProcessSeries { get; set; } = true;

        public bool RequireProviderId { get; set; } = true;

        public string? RadarrUrl { get; set; }

        public string? RadarrApiKey { get; set; }

        public string? SonarrUrl { get; set; }

        public string? SonarrApiKey { get; set; }
    }

    public sealed class ConfigResponse
    {
        public bool Enabled { get; set; }

        public bool DryRun { get; set; }

        public bool ProcessMovies { get; set; }

        public bool ProcessSeries { get; set; }

        public bool RequireProviderId { get; set; }

        public string RadarrUrl { get; set; } = string.Empty;

        public bool HasRadarrApiKey { get; set; }

        public string SonarrUrl { get; set; } = string.Empty;

        public bool HasSonarrApiKey { get; set; }
    }
}
