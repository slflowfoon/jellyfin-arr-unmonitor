using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace ArrUnmonitor.Services;

public interface IArrClient
{
    Task UnmonitorMovieAsync(BaseItem item, CancellationToken cancellationToken);

    Task UnmonitorSeriesAsync(BaseItem item, CancellationToken cancellationToken);

    Task<bool> UnmonitorSportarrEventAsync(
        BaseItem item,
        IReadOnlyList<BaseItem> deletedChildren,
        CancellationToken cancellationToken);
}
