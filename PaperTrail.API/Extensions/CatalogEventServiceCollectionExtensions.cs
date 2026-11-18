using PaperTrail.API.BackgroundServices;
using PaperTrail.API.Configuration.CatalogEvents;
using PaperTrail.Infrastructure.Messaging;

namespace PaperTrail.API.Extensions;

public static class CatalogEventServiceCollectionExtensions
{
    public static IServiceCollection AddPaperTrailCatalogEvents(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("CatalogEvents:Enabled")) return services;

        services.AddOptions<CatalogEventOptions>().Bind(configuration.GetSection("CatalogEvents"))
            .Validate(options => Uri.TryCreate(options.Uri, UriKind.Absolute, out var address)
                && address.Scheme is "amqp" or "amqps", "Configure CatalogEvents:Uri with an AMQP address.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Exchange)
                && !string.IsNullOrWhiteSpace(options.Queue)
                && !string.IsNullOrWhiteSpace(options.RoutingKey), "Configure catalog exchange, queue and routing key.")
            .Validate(options => options.RetryDelaySeconds is >= 1 and <= 60,
                "CatalogEvents:RetryDelaySeconds must be between 1 and 60.")
            .ValidateOnStart();
        services.AddScoped<CatalogEventHandler>();
        services.AddSingleton<CatalogEventConsumer>();
        services.AddHostedService(provider => provider.GetRequiredService<CatalogEventConsumer>());
        services.AddHealthChecks().AddCheck<CatalogEventConsumer>("catalogEvents");
        return services;
    }
}
