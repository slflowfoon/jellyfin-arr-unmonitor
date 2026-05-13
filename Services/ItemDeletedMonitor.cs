using System;
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
        _logger.LogInformation("Arr Unmonitor deletion monitor started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved -= OnItemRemoved;
        return Task.CompletedTask;
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs args)
    {
        if (args.Item.IsVirtualItem)
        {
            return;
        }

        _ = HandleItemRemovedAsync(args.Item);
    }

    private async Task HandleItemRemovedAsync(BaseItem item)
    {
        try
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            if (!config.Enabled)
            {
                return;
            }

            var typeName = item.GetType().Name;
            if (config.ProcessMovies && string.Equals(typeName, "Movie", StringComparison.Ordinal))
            {
                await _arrClient.UnmonitorMovieAsync(item, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (config.ProcessSeries && string.Equals(typeName, "Series", StringComparison.Ordinal))
            {
                await _arrClient.UnmonitorSeriesAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deleted Jellyfin item {ItemName} ({ItemId})", item.Name, item.Id);
        }
    }
}
