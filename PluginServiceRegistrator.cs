using ArrUnmonitor.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ArrUnmonitor;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IDeleteMediaRequestDetector, JellyfinDeleteMediaRequestDetector>();
        serviceCollection.AddHttpClient<IArrClient, ArrClient>();
        serviceCollection.AddHostedService<ItemDeletedMonitor>();
    }
}
