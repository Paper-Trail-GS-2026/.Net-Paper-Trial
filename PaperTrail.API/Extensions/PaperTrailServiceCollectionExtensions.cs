using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.Repositories;
using PaperTrail.Application.Services.Implementations;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Infrastructure.HttpClients;
using PaperTrail.Infrastructure.Persistence;
using PaperTrail.Infrastructure.Persistence.Repositories;

namespace PaperTrail.API.Extensions;

public static class PaperTrailServiceCollectionExtensions
{
    public static IServiceCollection AddPaperTrailDbContext(this IServiceCollection services)
    {
        services.AddDbContext<PaperTrailContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("PaperTrailOracle");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:PaperTrailOracle.");
            options.UseOracle(connectionString, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });
        return services;
    }

    public static IServiceCollection AddPaperTrailRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPreparationRepository, PreparationRepository>();
        services.AddScoped<IServiceVersionRepository, ServiceVersionRepository>();
        return services;
    }

    public static IServiceCollection AddPaperTrailApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPreparationService, PreparationService>();
        return services;
    }

    public static IServiceCollection AddPaperTrailCatalogClient(this IServiceCollection services)
    {
        services.AddHttpClient<IJavaCatalogClient, JavaCatalogClient>((provider, client) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var url = configuration["JavaCatalog:BaseUrl"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                || !address.AbsolutePath.EndsWith('/'))
                throw new InvalidOperationException("Configure JavaCatalog:BaseUrl as an HTTP URL ending with '/'.");
            client.BaseAddress = address;
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        return services;
    }
}
