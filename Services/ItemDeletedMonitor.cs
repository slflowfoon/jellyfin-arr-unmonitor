using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArrUnmonitor.Services;

public class ItemDeletedMonitor : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IArrClient _arrClient;
    private readonly ILogger<ItemDeletedMonitor> _logger;
    private readonly object _cacheLock = new();
    private readonly Dictionary<Guid, BaseItem> _fileItemCache = [];

    public ItemDeletedMonitor(
        ILibraryManager libraryManager,
        IArrClient arrClient,
        ILogger<ItemDeletedMonitor> logger)
    {
        _libraryManager = libraryManager;
        _arrClient = arrClient;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved += OnItemRemoved;
        _libraryManager.ItemAdded += OnItemAddedOrUpdated;
        _libraryManager.ItemUpdated += OnItemAddedOrUpdated;

        var cachedItemCount = 0;
        try
        {
            lock (_cacheLock)
            {
                foreach (var item in GetInitialFileItems())
                {
                    CacheItem(item);
                }

                cachedItemCount = _fileItemCache.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Arr Unmonitor could not initialize its folder-deletion cache; individual item monitoring will remain active");
        }

        _logger.LogInformation("Arr Unmonitor deletion monitor started with {ItemCount} file paths cached", cachedItemCount);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved -= OnItemRemoved;
        _libraryManager.ItemAdded -= OnItemAddedOrUpdated;
        _libraryManager.ItemUpdated -= OnItemAddedOrUpdated;
        return Task.CompletedTask;
    }

    private void OnItemAddedOrUpdated(object? sender, ItemChangeEventArgs args)
    {
        lock (_cacheLock)
        {
            CacheItem(args.Item);
        }
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs args)
    {
        if (args.Item.IsVirtualItem)
        {
            return;
        }

        var deletedChildren = TakeDeletedChildren(args.Item);
        _ = HandleItemRemovedAsync(args.Item, deletedChildren);
    }

    private async Task HandleItemRemovedAsync(BaseItem item, IReadOnlyList<BaseItem> deletedChildren)
    {
        try
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            if (!config.Enabled)
            {
                _logger.LogInformation("Arr Unmonitor is disabled; ignoring deleted Jellyfin item {ItemName} ({ItemType})", item.Name, item.GetType().Name);
                return;
            }

            var typeName = item.GetType().Name;
            _logger.LogInformation(
                "Arr Unmonitor saw deleted Jellyfin item {ItemName} ({ItemType}) at {Path}",
                item.Name,
                typeName,
                item.Path ?? "unknown path");

            if (config.ProcessMovies && string.Equals(typeName, "Movie", StringComparison.Ordinal))
            {
                await _arrClient.UnmonitorMovieAsync(item, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (config.ProcessSeries && string.Equals(typeName, "Series", StringComparison.Ordinal))
            {
                await _arrClient.UnmonitorSeriesAsync(item, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (config.ProcessSportarr)
            {
                await _arrClient.UnmonitorSportarrEventAsync(item, deletedChildren, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation("Arr Unmonitor ignored deleted Jellyfin item {ItemName} ({ItemType})", item.Name, typeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deleted Jellyfin item {ItemName} ({ItemId})", item.Name, item.Id);
        }
    }

    private void CacheItem(BaseItem item)
    {
        if (item.IsVirtualItem || item.IsFolder || string.IsNullOrWhiteSpace(item.Path))
        {
            _fileItemCache.Remove(item.Id);
            return;
        }

        _fileItemCache[item.Id] = item;
    }

    private IReadOnlyList<BaseItem> GetInitialFileItems()
    {
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsFolder = false,
            IsVirtualItem = false,
            GroupByPresentationUniqueKey = false
        };

        // Jellyfin 10.11 changed only the return type of this method, which is
        // binary-incompatible with plugins built against 10.10. Resolve it at
        // runtime so one plugin build can safely support both versions.
        var getItemList = _libraryManager.GetType().GetMethod("GetItemList", [typeof(InternalItemsQuery)]);
        if (getItemList is null)
        {
            throw new MissingMethodException(_libraryManager.GetType().FullName, "GetItemList(InternalItemsQuery)");
        }

        var result = getItemList.Invoke(_libraryManager, [query]);
        return result is IEnumerable<BaseItem> items
            ? items.ToList()
            : throw new InvalidOperationException("Jellyfin GetItemList returned an unsupported result type");
    }

    private IReadOnlyList<BaseItem> TakeDeletedChildren(BaseItem item)
    {
        lock (_cacheLock)
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                _fileItemCache.Remove(item.Id);
                return [];
            }

            var deletedPath = NormalizePath(item.Path).TrimEnd('/');
            var descendants = item.IsFolder
                ? _fileItemCache.Values.Where(candidate => PathIsUnder(candidate.Path, deletedPath)).ToList()
                : [];

            foreach (var cachedItem in _fileItemCache
                .Where(pair => pair.Key == item.Id || PathIsUnder(pair.Value.Path, deletedPath))
                .Select(pair => pair.Key)
                .ToList())
            {
                _fileItemCache.Remove(cachedItem);
            }

            return descendants;
        }
    }

    private static bool PathIsUnder(string? candidatePath, string parentPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var normalizedCandidate = NormalizePath(candidatePath).TrimEnd('/');
        return normalizedCandidate.StartsWith(parentPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}
