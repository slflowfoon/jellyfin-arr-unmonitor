using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        _logger.LogInformation(
            "Arr Unmonitor Radarr config state before processing {ItemName}: URL configured: {HasUrl}; API key configured: {HasApiKey}; Dry run: {DryRun}",
            item.Name,
            !string.IsNullOrWhiteSpace(config.RadarrUrl),
            !string.IsNullOrWhiteSpace(config.RadarrApiKey),
            config.DryRun);

        if (string.IsNullOrWhiteSpace(config.RadarrUrl) || string.IsNullOrWhiteSpace(config.RadarrApiKey))
        {
            _logger.LogWarning(
                "Radarr is not configured; skipping deleted movie {ItemName}. URL configured: {HasUrl}; API key configured: {HasApiKey}",
                item.Name,
                !string.IsNullOrWhiteSpace(config.RadarrUrl),
                !string.IsNullOrWhiteSpace(config.RadarrApiKey));
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
        _logger.LogInformation(
            "Arr Unmonitor Sonarr config state before processing {ItemName}: URL configured: {HasUrl}; API key configured: {HasApiKey}; Dry run: {DryRun}",
            item.Name,
            !string.IsNullOrWhiteSpace(config.SonarrUrl),
            !string.IsNullOrWhiteSpace(config.SonarrApiKey),
            config.DryRun);

        if (string.IsNullOrWhiteSpace(config.SonarrUrl) || string.IsNullOrWhiteSpace(config.SonarrApiKey))
        {
            _logger.LogWarning(
                "Sonarr is not configured; skipping deleted series {ItemName}. URL configured: {HasUrl}; API key configured: {HasApiKey}",
                item.Name,
                !string.IsNullOrWhiteSpace(config.SonarrUrl),
                !string.IsNullOrWhiteSpace(config.SonarrApiKey));
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

    public async Task<bool> UnmonitorSportarrEventAsync(
        BaseItem item,
        IReadOnlyList<BaseItem> deletedChildren,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        _logger.LogInformation(
            "Arr Unmonitor Sportarr config state before processing {ItemName}: URL configured: {HasUrl}; API key configured: {HasApiKey}; Dry run: {DryRun}",
            item.Name,
            !string.IsNullOrWhiteSpace(config.SportarrUrl),
            !string.IsNullOrWhiteSpace(config.SportarrApiKey),
            config.DryRun);

        if (string.IsNullOrWhiteSpace(config.SportarrUrl) || string.IsNullOrWhiteSpace(config.SportarrApiKey))
        {
            _logger.LogWarning(
                "Sportarr is not configured; skipping deleted item {ItemName}. URL configured: {HasUrl}; API key configured: {HasApiKey}",
                item.Name,
                !string.IsNullOrWhiteSpace(config.SportarrUrl),
                !string.IsNullOrWhiteSpace(config.SportarrApiKey));
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            _logger.LogWarning("Deleted Jellyfin item {ItemName} has no path; cannot match Sportarr event", item.Name);
            return false;
        }

        using var request = CreateSportarrRequest(HttpMethod.Get, config.SportarrUrl, "/events", config.SportarrApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "fetch Sportarr events", cancellationToken).ConfigureAwait(false);

        var events = await response.Content.ReadFromJsonAsync<List<SportarrEvent>>(JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        var deletedPath = NormalizePath(item.Path);
        var seasonFolderKey = GetSeasonFolderKey(item);
        var folderLeagueName = item.IsFolder ? GetFolderLeagueName(item.Path) : null;
        var sportarrEvents = events
            .Where(candidate =>
                PathMatches(candidate.FilePath, deletedPath) ||
                PathIsUnder(candidate.FilePath, deletedPath) ||
                (candidate.Files?.Any(file => PathMatches(file.FilePath, deletedPath) || PathIsUnder(file.FilePath, deletedPath)) ?? false))
            .ToList();

        if (!string.IsNullOrWhiteSpace(folderLeagueName) && deletedChildren.Count > 0)
        {
            sportarrEvents.AddRange(FindSportarrEventsByDeletedChildren(events, deletedChildren, folderLeagueName));
        }
        else if (sportarrEvents.Count == 0)
        {
            var sportarrEvent = FindSportarrEventByEpisodeMetadata(events, item);
            if (sportarrEvent is not null)
            {
                sportarrEvents.Add(sportarrEvent);
            }
        }

        sportarrEvents = sportarrEvents
            .GroupBy(candidate => candidate.Id)
            .Select(group => group.First())
            .ToList();

        if (sportarrEvents.Count == 0)
        {
            var episodeKey = GetEpisodeKey(item);
            _logger.LogInformation(
                "No Sportarr event matched deleted Jellyfin item {ItemName} at {Path}; extracted episode season={Season}, episode={Episode}; folder season={FolderSeason}, league={FolderLeague}",
                item.Name,
                item.Path,
                episodeKey?.Season.ToString(CultureInfo.InvariantCulture) ?? "none",
                episodeKey?.Episode.ToString(CultureInfo.InvariantCulture) ?? "none",
                seasonFolderKey?.Season.ToString(CultureInfo.InvariantCulture) ?? "none",
                folderLeagueName ?? "none");

            if (!string.IsNullOrWhiteSpace(folderLeagueName) && deletedChildren.Count > 0)
            {
                _logger.LogWarning(
                    "Deleted Jellyfin folder {Path} had {ChildCount} cached episodes, but none matched Sportarr league {LeagueName}; refusing a broad Sportarr match",
                    item.Path,
                    deletedChildren.Count,
                    folderLeagueName);
            }

            return false;
        }

        if (sportarrEvents.Count > 1)
        {
            _logger.LogInformation("Matched {Count} Sportarr events for deleted Jellyfin folder/item {ItemName}", sportarrEvents.Count, item.Name);
        }

        var failureCount = 0;
        foreach (var sportarrEvent in sportarrEvents)
        {
            if (!sportarrEvent.Monitored)
            {
                _logger.LogInformation("Sportarr event {Title} is already unmonitored", sportarrEvent.Title);
                continue;
            }

            if (config.DryRun)
            {
                _logger.LogInformation("Dry run: would unmonitor Sportarr event {Title} ({SportarrEventId})", sportarrEvent.Title, sportarrEvent.Id);
                continue;
            }

            try
            {
                using var update = CreateSportarrRequest(HttpMethod.Put, config.SportarrUrl, $"/events/{sportarrEvent.Id}", config.SportarrApiKey);
                update.Content = JsonContent.Create(new { monitored = false }, options: JsonOptions);
                using var updateResponse = await _httpClient.SendAsync(update, cancellationToken).ConfigureAwait(false);
                await EnsureSuccessAsync(updateResponse, $"unmonitor Sportarr event {sportarrEvent.Id}", cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Unmonitored Sportarr event {Title} after Jellyfin deletion", sportarrEvent.Title);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "Failed to unmonitor Sportarr event {Title} ({SportarrEventId}); continuing with remaining matches", sportarrEvent.Title, sportarrEvent.Id);
            }
        }

        if (failureCount > 0)
        {
            _logger.LogWarning(
                "Failed to unmonitor {FailureCount} of {MatchCount} matched Sportarr events for deleted Jellyfin item {ItemName}",
                failureCount,
                sportarrEvents.Count,
                item.Name);
        }

        return true;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        var root = baseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(method, root + path);
        request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    private static HttpRequestMessage CreateSportarrRequest(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        var root = baseUrl.TrimEnd('/');
        var apiPath = root.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? path : "/api" + path;
        var request = new HttpRequestMessage(method, root + apiPath);
        request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    private static bool PathMatches(string? candidatePath, string deletedPath)
    {
        return !string.IsNullOrWhiteSpace(candidatePath) &&
            string.Equals(NormalizePath(candidatePath), deletedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathIsUnder(string? candidatePath, string deletedPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var normalizedCandidate = NormalizePath(candidatePath).TrimEnd('/');
        var normalizedDeleted = NormalizePath(deletedPath).TrimEnd('/');
        return normalizedCandidate.StartsWith(normalizedDeleted + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private SportarrEvent? FindSportarrEventByEpisodeMetadata(IEnumerable<SportarrEvent> events, BaseItem item)
    {
        var episodeKey = GetEpisodeKey(item);
        if (episodeKey is null)
        {
            return null;
        }

        var candidates = events
            .Where(candidate => candidate.SeasonNumber == episodeKey.Value.Season && candidate.EpisodeNumber == episodeKey.Value.Episode)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogInformation(
                "No Sportarr events had season={Season}, episode={Episode} for deleted Jellyfin item {ItemName}",
                episodeKey.Value.Season,
                episodeKey.Value.Episode,
                item.Name);
            return null;
        }

        if (candidates.Count == 1)
        {
            _logger.LogInformation(
                "Matched Sportarr event {Title} by season={Season}, episode={Episode}",
                candidates[0].Title,
                episodeKey.Value.Season,
                episodeKey.Value.Episode);
            return candidates[0];
        }

        var pathAndName = ((item.Path ?? string.Empty) + " " + item.Name).ToLowerInvariant();
        var titleMatches = candidates
            .Where(candidate => TextContains(pathAndName, candidate.Title) || TextContains(candidate.Title, item.Name))
            .ToList();

        if (titleMatches.Count == 1)
        {
            _logger.LogInformation(
                "Matched Sportarr event {Title} by season={Season}, episode={Episode}, and title",
                titleMatches[0].Title,
                episodeKey.Value.Season,
                episodeKey.Value.Episode);
            return titleMatches[0];
        }

        var leagueMatches = titleMatches.Count > 0 ? titleMatches : candidates;
        leagueMatches = leagueMatches
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.LeagueName) && pathAndName.Contains(candidate.LeagueName.ToLowerInvariant(), StringComparison.Ordinal))
            .ToList();

        if (leagueMatches.Count == 1)
        {
            _logger.LogInformation(
                "Matched Sportarr event {Title} by season={Season}, episode={Episode}, and league {LeagueName}",
                leagueMatches[0].Title,
                episodeKey.Value.Season,
                episodeKey.Value.Episode,
                leagueMatches[0].LeagueName);
            return leagueMatches[0];
        }

        _logger.LogWarning(
            "Found {Count} Sportarr events with season={Season}, episode={Episode} for deleted Jellyfin item {ItemName}; refusing ambiguous match",
            candidates.Count,
            episodeKey.Value.Season,
            episodeKey.Value.Episode,
            item.Name);
        return null;
    }

    private List<SportarrEvent> FindSportarrEventsByDeletedChildren(
        IEnumerable<SportarrEvent> events,
        IReadOnlyList<BaseItem> deletedChildren,
        string leagueName)
    {
        var normalizedLeagueName = NormalizeText(leagueName);
        var eventList = events
            .Where(candidate => string.Equals(
                NormalizeText(candidate.LeagueName),
                normalizedLeagueName,
                StringComparison.Ordinal))
            .ToList();
        var matches = new List<SportarrEvent>();

        foreach (var child in deletedChildren)
        {
            var match = FindSportarrEventByEpisodeMetadata(eventList, child);
            if (match is not null)
            {
                matches.Add(match);
            }
        }

        matches = matches
            .GroupBy(candidate => candidate.Id)
            .Select(group => group.First())
            .ToList();

        if (matches.Count > 0)
        {
            _logger.LogInformation(
                "Matched {Count} Sportarr events in league {LeagueName} from {ChildCount} cached Jellyfin files under the deleted folder",
                matches.Count,
                leagueName,
                deletedChildren.Count);
        }

        return matches;
    }

    private static (int Season, int Episode)? GetEpisodeKey(BaseItem item)
    {
        var season = GetIntProperty(item, "ParentIndexNumber") ?? GetIntProperty(item, "SeasonNumber");
        var episode = GetIntProperty(item, "IndexNumber") ?? GetIntProperty(item, "EpisodeNumber");

        if (season is not null && episode is not null)
        {
            return (season.Value, episode.Value);
        }

        var source = (item.Path ?? string.Empty) + " " + item.Name;
        var match = Regex.Match(source, @"\bS(?<season>\d{1,4})E(?<episode>\d{1,4})\b", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups["season"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeason) &&
            int.TryParse(match.Groups["episode"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEpisode)
            ? (parsedSeason, parsedEpisode)
            : null;
    }

    private static (int Season, string LeagueName)? GetSeasonFolderKey(BaseItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return null;
        }

        var segments = NormalizePath(item.Path)
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var seasonMatch = Regex.Match(segments[^1], @"^Season\s+(?<season>\d{4})$", RegexOptions.IgnoreCase);
        if (!seasonMatch.Success ||
            !int.TryParse(seasonMatch.Groups["season"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var season))
        {
            return null;
        }

        return (season, segments[^2]);
    }

    private static string? GetFolderLeagueName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = NormalizePath(path)
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        for (var i = 1; i < segments.Length; i++)
        {
            if (Regex.IsMatch(segments[i], @"^Season\s+\d{4}$", RegexOptions.IgnoreCase))
            {
                return segments[i - 1];
            }
        }

        return segments[^1];
    }

    private static int? GetIntProperty(BaseItem item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(item);
        return value switch
        {
            int intValue => intValue,
            _ => null
        };
    }

    private static bool TextContains(string? haystack, string? needle)
    {
        var normalizedHaystack = NormalizeText(haystack);
        var normalizedNeedle = NormalizeText(needle);
        return normalizedNeedle.Length > 0 && normalizedHaystack.Contains(normalizedNeedle, StringComparison.Ordinal);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
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

    private sealed class SportarrEvent
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        public string? LeagueName { get; set; }

        public int? SeasonNumber { get; set; }

        public int? EpisodeNumber { get; set; }

        public bool Monitored { get; set; }

        public string? FilePath { get; set; }

        public List<SportarrEventFile>? Files { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class SportarrEventFile
    {
        public string? FilePath { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
