using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaperTrail.API.Tests.Support;
using PaperTrail.Infrastructure.Health;
using PaperTrail.Infrastructure.Persistence;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class HealthEndpointTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // Real SQLite database for the check; the catalog answers with the given handler.
    private static WebApplicationFactory<Program> Factory(PreparationCreationApiFactory root,
        Func<HttpResponseMessage> catalog, bool databaseDown = false, Action<IConfigurationBuilder>? configure = null) =>
        root.WithWebHostBuilder(builder =>
        {
            if (configure is not null) builder.ConfigureAppConfiguration((_, configuration) => configure(configuration));
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<CatalogHealthCheck>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(catalog));
                if (!databaseDown) return;
                services.RemoveAll<PaperTrailContext>();
                services.AddScoped(_ => new PaperTrailContext(new DbContextOptionsBuilder<PaperTrailContext>()
                    .UseSqlite("Data Source=/papertrail-missing-directory/db.sqlite;Mode=ReadOnly").Options));
            });
        });

    private static async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> Get(HttpClient client)
    {
        using var response = await client.GetAsync("/health");
        var raw = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(raw).RootElement, raw);
    }

    [Fact]
    public async Task HealthyWhenDatabaseAndCatalogAnswer()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => new HttpResponseMessage(HttpStatusCode.OK));
        var (status, body, _) = await Get(Client(factory));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("catalog").GetString());
    }

    [Fact]
    public async Task CatalogAnsweringWith401StillCountsAsReachable()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var (status, body, _) = await Get(Client(factory));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DegradedWhenTheCatalogIsUnreachableButTheDatabaseWorks()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => throw new HttpRequestException("connection refused"));
        var (status, body, _) = await Get(Client(factory));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Degraded", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("Degraded", body.GetProperty("checks").GetProperty("catalog").GetString());
    }

    [Fact]
    public async Task DegradedWhenTheCatalogReturnsAServerError()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (status, body, _) = await Get(Client(factory));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Degraded", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnhealthyWith503WhenTheDatabaseIsDown()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => new HttpResponseMessage(HttpStatusCode.OK), databaseDown: true);
        var (status, body, _) = await Get(Client(factory));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("catalog").GetString());
    }

    [Fact]
    public async Task ResponseOnlyContainsStatusNamesAndNoInternalDetails()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => throw new HttpRequestException("secret-host:9999 refused"), databaseDown: true);
        var (_, body, raw) = await Get(Client(factory));
        Assert.Equal(["checks", "status"], body.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.DoesNotContain("secret-host", raw);
        Assert.DoesNotContain("papertrail-missing-directory", raw);
        Assert.DoesNotContain("Exception", raw);
    }

    [Fact]
    public async Task HealthIsPublicAndNotRateLimited()
    {
        using var root = new PreparationCreationApiFactory();
        using var factory = Factory(root, () => new HttpResponseMessage(HttpStatusCode.OK),
            configure: configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "2" }));
        using var client = Client(factory);
        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
