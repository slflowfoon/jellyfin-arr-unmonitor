using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArrUnmonitor.Api;

[ApiController]
[Route("plugins/ArrUnmonitor")]
public class ClientScriptController : ControllerBase
{
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    public IActionResult GetClientScript()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"{typeof(Plugin).Namespace}.Web.main.js");

        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return Content(reader.ReadToEnd(), "application/javascript; charset=utf-8");
    }
}
