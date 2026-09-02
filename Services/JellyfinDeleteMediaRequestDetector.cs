using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ArrUnmonitor.Services;

internal sealed class JellyfinDeleteMediaRequestDetector : IDeleteMediaRequestDetector
{
    private const string LibraryControllerTypeName = "Jellyfin.Api.Controllers.LibraryController";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JellyfinDeleteMediaRequestDetector(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsDeleteMediaRequest(Guid itemId)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null || !HttpMethods.IsDelete(context.Request.Method))
        {
            return false;
        }

        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null ||
            !string.Equals(action.ControllerTypeInfo.FullName, LibraryControllerTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        return action.MethodInfo.Name switch
        {
            "DeleteItem" => RouteItemMatches(context, itemId),
            "DeleteItems" => QueryItemsContain(context, itemId),
            _ => false
        };
    }

    private static bool RouteItemMatches(HttpContext context, Guid itemId)
    {
        return context.Request.RouteValues.TryGetValue("itemId", out var routeValue) &&
            Guid.TryParse(Convert.ToString(routeValue), out var routeItemId) &&
            routeItemId == itemId;
    }

    private static bool QueryItemsContain(HttpContext context, Guid itemId)
    {
        foreach (var queryValue in context.Request.Query["ids"])
        {
            if (string.IsNullOrWhiteSpace(queryValue))
            {
                continue;
            }

            foreach (var value in queryValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(value, out var queryItemId) && queryItemId == itemId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
