using BusinessLayer.Channels;
using BusinessLayer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLayer;

public static class BusinessLayerServiceRegistration
{
    /// <summary>
    /// Extension method for registering the services in the BusinessLayer project to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to which the services will be added.</param>
    public static void AddBusinessLayerServices(this IServiceCollection services)
    {
        // Register the InputProcessingService as a transient service
        services.AddTransient<IInputProcessingService, InputProcessingService>();

        // Add channel services
        services.AddSingleton<IDataProcessingRequestChannel, DataProcessingRequestChannel>();
        services.AddSingleton<IDataProcessingChannel, DataProcessingChannel>();
    }
}
