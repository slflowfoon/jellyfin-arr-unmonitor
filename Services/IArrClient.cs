using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace ArrUnmonitor.Services;

public interface IArrClient
{
    Task<ConnectionTestResult> TestConnectionAsync(
        string? service,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken);

    Task UnmonitorMovieAsync(BaseItem item, CancellationToken cancellationToken);

    Task UnmonitorSeriesAsync(BaseItem item, CancellationToken cancellationToken);

    Task<bool> UnmonitorSportarrEventAsync(
        BaseItem item,
        IReadOnlyList<BaseItem> deletedChildren,
        CancellationToken cancellationToken);

    Task RemoveFromSeerrAsync(BaseItem item, string mediaType, CancellationToken cancellationToken);
}
