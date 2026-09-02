using System;
using System.Reflection;
using ArrUnmonitor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace ArrUnmonitor.Tests
{
    public class JellyfinDeleteMediaRequestDetectorTests
    {
        [Fact]
        public void SingleDeleteMatchesRouteItem()
        {
            var itemId = Guid.NewGuid();
            var context = CreateContext(nameof(Jellyfin.Api.Controllers.LibraryController.DeleteItem));
            context.Request.RouteValues["itemId"] = itemId.ToString("N");

            Assert.True(CreateDetector(context).IsDeleteMediaRequest(itemId));
        }

        [Fact]
        public void SingleDeleteRejectsDifferentRouteItem()
        {
            var context = CreateContext(nameof(Jellyfin.Api.Controllers.LibraryController.DeleteItem));
            context.Request.RouteValues["itemId"] = Guid.NewGuid();

            Assert.False(CreateDetector(context).IsDeleteMediaRequest(Guid.NewGuid()));
        }

        [Fact]
        public void BulkDeleteMatchesCommaDelimitedAndRepeatedQueryItems()
        {
            var firstItemId = Guid.NewGuid();
            var secondItemId = Guid.NewGuid();
            var context = CreateContext(nameof(Jellyfin.Api.Controllers.LibraryController.DeleteItems));
            context.Request.QueryString = new QueryString($"?ids={firstItemId}&ids={Guid.NewGuid()},{secondItemId}");

            var detector = CreateDetector(context);

            Assert.True(detector.IsDeleteMediaRequest(firstItemId));
            Assert.True(detector.IsDeleteMediaRequest(secondItemId));
        }

        [Fact]
        public void BulkDeleteRejectsMissingOrMalformedQueryItems()
        {
            var context = CreateContext(nameof(Jellyfin.Api.Controllers.LibraryController.DeleteItems));
            context.Request.QueryString = new QueryString("?ids=not-a-guid");

            Assert.False(CreateDetector(context).IsDeleteMediaRequest(Guid.NewGuid()));
        }

        [Fact]
        public void BackgroundRemovalWithoutHttpContextIsRejected()
        {
            Assert.False(CreateDetector(null).IsDeleteMediaRequest(Guid.NewGuid()));
        }

        [Theory]
        [InlineData("POST", "DeleteItem")]
        [InlineData("DELETE", "RefreshLibrary")]
        public void UnrelatedRequestsAreRejected(string method, string actionName)
        {
            var itemId = Guid.NewGuid();
            var context = CreateContext(actionName, method);
            context.Request.RouteValues["itemId"] = itemId;

            Assert.False(CreateDetector(context).IsDeleteMediaRequest(itemId));
        }

        [Fact]
        public void DifferentControllerIsRejected()
        {
            var itemId = Guid.NewGuid();
            var context = CreateContext(nameof(OtherController.DeleteItem), controllerType: typeof(OtherController));
            context.Request.RouteValues["itemId"] = itemId;

            Assert.False(CreateDetector(context).IsDeleteMediaRequest(itemId));
        }

        [Fact]
        public void MissingEndpointMetadataIsRejected()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Delete;

            Assert.False(CreateDetector(context).IsDeleteMediaRequest(Guid.NewGuid()));
        }

        private static JellyfinDeleteMediaRequestDetector CreateDetector(HttpContext? context)
        {
            return new JellyfinDeleteMediaRequestDetector(new HttpContextAccessor { HttpContext = context });
        }

        private static DefaultHttpContext CreateContext(
            string actionName,
            string method = "DELETE",
            Type? controllerType = null)
        {
            controllerType ??= typeof(Jellyfin.Api.Controllers.LibraryController);
            var methodInfo = controllerType.GetMethod(actionName) ?? throw new InvalidOperationException($"Missing test action {actionName}");
            var descriptor = new ControllerActionDescriptor
            {
                ActionName = actionName,
                ControllerName = controllerType.Name.Replace("Controller", string.Empty, StringComparison.Ordinal),
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = methodInfo
            };

            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(descriptor), actionName));
            return context;
        }

        private sealed class OtherController
        {
            public void DeleteItem()
            {
            }
        }
    }
}

namespace Jellyfin.Api.Controllers
{
    internal sealed class LibraryController
    {
        public void DeleteItem()
        {
        }

        public void DeleteItems()
        {
        }

        public void RefreshLibrary()
        {
        }
    }
}
