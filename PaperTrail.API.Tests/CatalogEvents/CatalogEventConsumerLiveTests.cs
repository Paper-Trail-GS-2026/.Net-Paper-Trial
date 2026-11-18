using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaperTrail.API.Configuration.CatalogEvents;
using PaperTrail.API.Extensions;
using PaperTrail.API.Tests.Support;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Entities;
using PaperTrail.Infrastructure.Persistence;
using PaperTrail.Infrastructure.Persistence.Repositories;
using RabbitMQ.Client;

namespace PaperTrail.API.Tests.CatalogEvents;

public sealed class CatalogEventConsumerLiveTests
{
    [RabbitMqFact]
    public async Task ConsumesJavaMessagesRetriesDatabaseFailuresAndPreservesTheChecklist()
    {
        using var root = new PreparationCreationApiFactory();
        long preparationId;
        await using (var db = root.Context())
        {
            var preparation = new Preparation("1000", 1, 2, "Personal preparation");
            db.Preparations.Add(preparation);
            await db.SaveChangesAsync();
            preparationId = preparation.Id;
            var item = new PreparationItem(preparationId, 11, "Personal document", true);
            item.Complete();
            db.PreparationItems.Add(item);
            await db.SaveChangesAsync();
        }

        var settings = Settings();
        var failures = new FailOnce();
        await using var connection = await new ConnectionFactory { Uri = new Uri(settings.Uri) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true));
        using var app = App(root, settings, failures);
        using var client = Client(app, root);
        try
        {
            await WaitFor(() => Connected(client));
            await Publish(channel, settings, CatalogEventHandlerTests.Message(1, 5));
            await WaitFor(() => HasVersion(root, 5));
            Assert.True(failures.Triggered);

            var repeated = CatalogEventHandlerTests.Message(1, 5);
            await Publish(channel, settings, repeated);
            await Publish(channel, settings, repeated);
            await Publish(channel, settings, CatalogEventHandlerTests.Message(1, 3));
            await Publish(channel, settings, Encoding.UTF8.GetBytes("{\"invalid\":true}"));
            await Publish(channel, settings, CatalogEventHandlerTests.Message(1, 6));
            await WaitFor(() => HasVersion(root, 6));

            using var progress = JsonDocument.Parse(await client.GetStringAsync($"/preparativos/{preparationId}/progresso"));
            Assert.True(progress.RootElement.GetProperty("sourceOutdated").GetBoolean());
            Assert.True(progress.RootElement.GetProperty("ready").GetBoolean());
            await using var check = root.Context();
            Assert.Equal(2, (await check.Preparations.SingleAsync()).SourceVersion);
            var item = await check.PreparationItems.SingleAsync();
            Assert.Equal("Personal document", item.Title);
            Assert.True(item.Completed);
            Assert.Equal(11, item.RequirementId);
        }
        finally
        {
            app.Dispose();
            await channel.QueueDeleteAsync(settings.Queue, ifUnused: false, ifEmpty: false);
            await channel.ExchangeDeleteAsync(settings.Exchange);
        }
    }

    [RabbitMqManagementFact]
    public async Task ReconnectsAfterTheBrokerClosesTheConsumerConnection()
    {
        using var root = new PreparationCreationApiFactory();
        var settings = Settings();
        await using var connection = await new ConnectionFactory { Uri = new Uri(settings.Uri) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true));
        using var app = App(root, settings);
        using var client = Client(app, root);
        try
        {
            await WaitFor(() => Connected(client));
            await Publish(channel, settings, CatalogEventHandlerTests.Message(1, 2));
            await WaitFor(() => HasVersion(root, 2));

            var address = new Uri(settings.Uri);
            using var management = new HttpClient
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable("PAPERTRAIL_RABBITMQ_TEST_MANAGEMENT_URI")!),
                Timeout = TimeSpan.FromSeconds(10)
            };
            management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(address.UserInfo))));
            var virtualHost = Uri.UnescapeDataString(address.AbsolutePath.TrimStart('/'));
            if (virtualHost.Length == 0) virtualHost = "/";
            string? connectionName = null;
            await WaitFor(async () =>
            {
                using var consumers = JsonDocument.Parse(await management.GetStringAsync("api/consumers/" + Uri.EscapeDataString(virtualHost)));
                var ownConsumer = consumers.RootElement.EnumerateArray()
                    .SingleOrDefault(consumer => consumer.GetProperty("queue").GetProperty("name").GetString() == settings.Queue);
                if (ownConsumer.ValueKind == JsonValueKind.Undefined) return false;
                connectionName = ownConsumer.GetProperty("channel_details").GetProperty("connection_name").GetString();
                return connectionName is not null;
            });
            using var closed = await management.DeleteAsync("api/connections/" + Uri.EscapeDataString(connectionName!));
            Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

            await Publish(channel, settings, CatalogEventHandlerTests.Message(1, 7));
            await WaitFor(() => HasVersion(root, 7));
            await WaitFor(() => Connected(client));
            await using var check = root.Context();
            Assert.Single(await check.ServiceVersions.ToListAsync());
        }
        finally
        {
            app.Dispose();
            await channel.QueueDeleteAsync(settings.Queue, ifUnused: false, ifEmpty: false);
            await channel.ExchangeDeleteAsync(settings.Exchange);
        }
    }

    [Fact]
    public async Task BrokerUnavailableDoesNotStopTheApiAndAppearsInHealth()
    {
        using var root = new PreparationCreationApiFactory();
        var settings = new CatalogEventOptions { Uri = "amqp://127.0.0.1:1", RetryDelaySeconds = 1 };
        using var app = App(root, settings);
        using var client = Client(app, root);
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Degraded", body.RootElement.GetProperty("checks").GetProperty("catalogEvents").GetString());
        Assert.DoesNotContain("amqp", body.RootElement.GetRawText());
    }

    private static CatalogEventOptions Settings() => new()
    {
        Uri = Environment.GetEnvironmentVariable("PAPERTRAIL_RABBITMQ_TEST_URI")!,
        Exchange = "papertrail.test." + Guid.NewGuid().ToString("N"),
        Queue = "papertrail.test." + Guid.NewGuid().ToString("N"),
        RetryDelaySeconds = 1
    };

    private static WebApplicationFactory<Program> App(PreparationCreationApiFactory root,
        CatalogEventOptions settings, FailOnce? failures = null) => root.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CatalogEvents:Enabled"] = "true", ["CatalogEvents:Uri"] = settings.Uri,
                ["CatalogEvents:Exchange"] = settings.Exchange, ["CatalogEvents:Queue"] = settings.Queue,
                ["CatalogEvents:RetryDelaySeconds"] = settings.RetryDelaySeconds.ToString()
            }).Build();
            services.AddPaperTrailCatalogEvents(config);
            if (failures is not null)
            {
                services.RemoveAll<IServiceVersionRepository>();
                services.AddScoped<IServiceVersionRepository>(provider => new FailOnceRepository(
                    new ServiceVersionRepository(provider.GetRequiredService<PaperTrailContext>()), failures));
            }
        }));

    private static HttpClient Client(WebApplicationFactory<Program> app, PreparationCreationApiFactory root)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", root.Token());
        return client;
    }

    private static async Task<bool> Connected(HttpClient client)
    {
        using var body = JsonDocument.Parse(await client.GetStringAsync("/health"));
        return body.RootElement.GetProperty("checks").GetProperty("catalogEvents").GetString() == "Healthy";
    }

    private static async Task<bool> HasVersion(PreparationCreationApiFactory root, long version)
    {
        await using var db = root.Context();
        return await db.ServiceVersions.AnyAsync(row => row.ServiceId == 1 && row.Version == version);
    }

    private static ValueTask Publish(IChannel channel, CatalogEventOptions settings, byte[] body) =>
        channel.BasicPublishAsync(settings.Exchange, settings.RoutingKey, mandatory: true,
            basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json" }, body: body);

    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }

    private sealed class FailOnce
    {
        public int Remaining = 1;
        public bool Triggered => Remaining == 0;
    }

    private sealed class FailOnceRepository(IServiceVersionRepository inner, FailOnce failures) : IServiceVersionRepository
    {
        public Task<long?> GetVersionAsync(long serviceId, CancellationToken cancellationToken) =>
            inner.GetVersionAsync(serviceId, cancellationToken);

        public Task UpdateIfNewerAsync(long serviceId, long version, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref failures.Remaining, 0) == 1)
                throw new IOException("Temporary database failure for the live test.");
            return inner.UpdateIfNewerAsync(serviceId, version, cancellationToken);
        }
    }
}

public class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAPERTRAIL_RABBITMQ_TEST_URI")))
            Skip = "Configure PAPERTRAIL_RABBITMQ_TEST_URI to run against RabbitMQ.";
    }
}

public sealed class RabbitMqManagementFactAttribute : RabbitMqFactAttribute
{
    public RabbitMqManagementFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAPERTRAIL_RABBITMQ_TEST_MANAGEMENT_URI")))
            Skip = "Configure PAPERTRAIL_RABBITMQ_TEST_MANAGEMENT_URI to test broker disconnection.";
    }
}
