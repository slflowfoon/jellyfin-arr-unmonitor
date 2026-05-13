using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging;

namespace ArrUnmonitor.Services;

public class ArrClient : IArrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ArrClient> _logger;

    public ArrClient(HttpClient httpClient, ILogger<ArrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task UnmonitorMovieAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (string.IsNullOrWhiteSpace(config.RadarrUrl) || string.IsNullOrWhiteSpace(config.RadarrApiKey))
        {
            _logger.LogWarning("Radarr is not configured; skipping deleted movie {ItemName}", item.Name);
            return;
        }

        var tmdbId = GetProviderInt(item, "Tmdb");
        var imdbId = GetProviderString(item, "Imdb");
        if (tmdbId is null && string.IsNullOrWhiteSpace(imdbId))
        {
            LogProviderMiss("Radarr", item, config.RequireProviderId);
            return;
        }

        _logger.LogInformation(
            "Looking for Radarr match for deleted Jellyfin movie {ItemName}; TMDb={TmdbId}, IMDb={ImdbId}",
            item.Name,
            tmdbId?.ToString(CultureInfo.InvariantCulture) ?? "none",
            string.IsNullOrWhiteSpace(imdbId) ? "none" : imdbId);

        using var request = CreateRequest(HttpMethod.Get, config.RadarrUrl, "/api/v3/movie", config.RadarrApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "fetch Radarr movies", cancellationToken).ConfigureAwait(false);

        var movies = await response.Content.ReadFromJsonAsync<List<RadarrMovie>>(JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        var movie = movies.FirstOrDefault(candidate => tmdbId is not null && candidate.TmdbId == tmdbId.Value)
            ?? movies.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(imdbId) && string.Equals(candidate.ImdbId, imdbId, StringComparison.OrdinalIgnoreCase));
        if (movie is null)
        {
            _logger.LogInformation(
                "No Radarr movie matched deleted Jellyfin movie {ItemName}; TMDb={TmdbId}, IMDb={ImdbId}",
                item.Name,
                tmdbId?.ToString(CultureInfo.InvariantCulture) ?? "none",
                string.IsNullOrWhiteSpace(imdbId) ? "none" : imdbId);
            return;
        }

        if (!movie.Monitored)
        {
            _logger.LogInformation("Radarr movie {Title} is already unmonitored", movie.Title);
            return;
        }

        movie.Monitored = false;
        if (config.DryRun)
        {
            _logger.LogInformation("Dry run: would unmonitor Radarr movie {Title} ({RadarrId})", movie.Title, movie.Id);
            return;
        }

        using var update = CreateRequest(HttpMethod.Put, config.RadarrUrl, $"/api/v3/movie/{movie.Id}", config.RadarrApiKey);
        update.Content = JsonContent.Create(movie, options: JsonOptions);
        using var updateResponse = await _httpClient.SendAsync(update, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(updateResponse, $"unmonitor Radarr movie {movie.Id}", cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Unmonitored Radarr movie {Title} after Jellyfin deletion", movie.Title);
    }

    public async Task UnmonitorSeriesAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (string.IsNullOrWhiteSpace(config.SonarrUrl) || string.IsNullOrWhiteSpace(config.SonarrApiKey))
        {
            _logger.LogWarning("Sonarr is not configured; skipping deleted series {ItemName}", item.Name);
            return;
        }

        var tvdbId = GetProviderInt(item, "Tvdb");
        if (tvdbId is null)
        {
            LogProviderMiss("Sonarr", item, config.RequireProviderId);
            return;
        }

        _logger.LogInformation("Looking for Sonarr match for deleted Jellyfin series {ItemName}; TVDb={TvdbId}", item.Name, tvdbId);

        using var request = CreateRequest(HttpMethod.Get, config.SonarrUrl, "/api/v3/series", config.SonarrApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "fetch Sonarr series", cancellationToken).ConfigureAwait(false);

        var seriesItems = await response.Content.ReadFromJsonAsync<List<SonarrSeries>>(JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        var series = seriesItems.FirstOrDefault(candidate => candidate.TvdbId == tvdbId.Value);
        if (series is null)
        {
            _logger.LogInformation("No Sonarr series matched deleted Jellyfin series {ItemName} with TVDb {TvdbId}", item.Name, tvdbId);
            return;
        }

        if (!series.Monitored)
        {
            _logger.LogInformation("Sonarr series {Title} is already unmonitored", series.Title);
            return;
        }

        series.Monitored = false;
        if (config.DryRun)
        {
            _logger.LogInformation("Dry run: would unmonitor Sonarr series {Title} ({SonarrId})", series.Title, series.Id);
            return;
        }

        using var update = CreateRequest(HttpMethod.Put, config.SonarrUrl, $"/api/v3/series/{series.Id}", config.SonarrApiKey);
        update.Content = JsonContent.Create(series, options: JsonOptions);
        using var updateResponse = await _httpClient.SendAsync(update, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(updateResponse, $"unmonitor Sonarr series {series.Id}", cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Unmonitored Sonarr series {Title} after Jellyfin deletion", series.Title);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        var root = baseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(method, root + path);
        request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    private static int? GetProviderInt(BaseItem item, string provider)
    {
        var raw = GetProviderString(item, provider);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string? GetProviderString(BaseItem item, string provider)
    {
        foreach (var (key, value) in item.ProviderIds)
        {
            if (string.Equals(key, provider, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private void LogProviderMiss(string target, BaseItem item, bool requireProviderId)
    {
        var providerIds = item.ProviderIds.Count == 0
            ? "none"
            : string.Join(", ", item.ProviderIds.Select(pair => pair.Key + "=" + pair.Value));

        if (requireProviderId)
        {
            _logger.LogWarning("Deleted Jellyfin item {ItemName} could not be matched in {Target}; provider IDs: {ProviderIds}", item.Name, target, providerIds);
        }
        else
        {
            _logger.LogWarning("Title matching is not implemented yet; deleted Jellyfin item {ItemName} could not be matched in {Target}; provider IDs: {ProviderIds}", item.Name, target, providerIds);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException($"Could not {action}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
    }

    private sealed class RadarrMovie
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        public int TmdbId { get; set; }

        public string? ImdbId { get; set; }

        public bool Monitored { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class SonarrSeries
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        public int TvdbId { get; set; }

        public bool Monitored { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
