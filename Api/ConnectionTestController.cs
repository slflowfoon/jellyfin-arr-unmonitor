using System.Threading;
using System.Threading.Tasks;
using ArrUnmonitor.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrUnmonitor.Api;

[ApiController]
[Route("plugins/ArrUnmonitor")]
[Authorize(Policy = Policies.RequiresElevation)]
public class ConnectionTestController : ControllerBase
{
    private readonly IArrClient _arrClient;

    public ConnectionTestController(IArrClient arrClient)
    {
        _arrClient = arrClient;
    }

    [HttpPost("TestConnection")]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection(
        [FromBody] ConnectionTestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _arrClient
            .TestConnectionAsync(request.Service, request.Url, request.ApiKey, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}

public sealed class ConnectionTestRequest
{
    public string? Service { get; set; }

    public string? Url { get; set; }

    public string? ApiKey { get; set; }
}
