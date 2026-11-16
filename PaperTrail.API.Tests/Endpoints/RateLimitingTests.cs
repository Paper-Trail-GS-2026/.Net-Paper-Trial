using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PaperTrail.API.Tests.Support;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class RateLimitingTests
{
    private static WebApplicationFactory<Program> Limited(PaperTrailApiFactory root, int permitLimit, int windowSeconds = 60) =>
        root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = permitLimit.ToString(),
                ["RateLimiting:WindowSeconds"] = windowSeconds.ToString()
            })));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task RequestsAboveTheLimitGet429WithRetryAfterAndProblemDetails()
    {
        using var root = new PaperTrailApiFactory();
        using var factory = Limited(root, 3);
        using var client = Client(factory, root.Token("1000"));
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/preparativos")).StatusCode);

        using var limited = await client.GetAsync("/preparativos");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(int.Parse(limited.Headers.GetValues("Retry-After").Single()) is > 0 and <= 60);
        Assert.StartsWith("application/problem+json", limited.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task TheLimitIsPerUserAndSharedAcrossEndpoints()
    {
        using var root = new PaperTrailApiFactory();
        using var factory = Limited(root, 2);
        using var first = Client(factory, root.Token("1000"));
        using var second = Client(factory, root.Token("2000"));
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/preparativos/101")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/preparativos")).StatusCode);
    }

    [Fact]
    public async Task AnonymousRequestsAreLimitedToo()
    {
        using var root = new PaperTrailApiFactory();
        using var factory = Limited(root, 2);
        using var client = Client(factory, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/preparativos")).StatusCode);
    }

    [Fact]
    public async Task ChecklistEndpointsShareTheSameLimit()
    {
        using var root = new PaperTrailApiFactory();
        using var factory = Limited(root, 1);
        using var client = Client(factory, root.Token("1000"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/preparativos/999/progresso")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/preparativos/999/itens")).StatusCode);
    }

    [Fact]
    public async Task TheLimitResetsWhenTheWindowEnds()
    {
        using var root = new PaperTrailApiFactory();
        using var factory = Limited(root, 1, windowSeconds: 1);
        using var client = Client(factory, root.Token("1000"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/preparativos")).StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/preparativos")).StatusCode);
    }
}
