using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PaperTrail.API.Tests.Support;
using PaperTrail.Domain.Entities;
using PaperTrail.Domain.Exceptions;
using PaperTrail.Infrastructure.Messaging;
using PaperTrail.Infrastructure.Persistence;
using PaperTrail.Infrastructure.Persistence.Repositories;

namespace PaperTrail.API.Tests.CatalogEvents;

public sealed class CatalogEventHandlerTests
{
    internal static byte[] Message(long serviceId, long version, Guid? eventId = null) =>
        Encoding.UTF8.GetBytes($$"""{"eventId":"{{eventId ?? Guid.NewGuid()}}","serviceId":{{serviceId}},"version":{{version}},"occurredAt":"2026-11-18T12:00:00Z"}""");

    [Fact]
    public async Task RepeatedAndOlderEventsKeepTheHighestVersionAndTimestamp()
    {
        using var factory = new PreparationCreationApiFactory();
        await using var db = factory.Context();
        var handler = new CatalogEventHandler(new ServiceVersionRepository(db));
        var message = Message(10, 5);
        await handler.HandleAsync(message, default);
        var first = await db.ServiceVersions.AsNoTracking().SingleAsync();

        await handler.HandleAsync(message, default);
        await handler.HandleAsync(Message(10, 3), default);
        var unchanged = await db.ServiceVersions.AsNoTracking().SingleAsync();
        Assert.Equal(5, unchanged.Version);
        Assert.Equal(first.UpdatedAt, unchanged.UpdatedAt);

        await handler.HandleAsync(Message(10, 7), default);
        var updated = await db.ServiceVersions.AsNoTracking().SingleAsync();
        Assert.Equal(7, updated.Version);
        Assert.True(updated.UpdatedAt >= first.UpdatedAt);
    }

    [Fact]
    public async Task ConcurrentFirstEventsKeepOnlyOneRowWithTheHighestVersion()
    {
        using var factory = new PreparationCreationApiFactory();
        await Task.WhenAll(new long[] { 4, 9, 2, 7, 9 }.Select(async version =>
        {
            await using var db = factory.Context();
            await new CatalogEventHandler(new ServiceVersionRepository(db)).HandleAsync(Message(10, version), default);
        }));
        await using var check = factory.Context();
        Assert.Equal(9, (await check.ServiceVersions.SingleAsync()).Version);
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(7, 7)]
    public async Task ConcurrentInsertIsRetriedWithoutOverwritingANewerVersion(long incoming, long expected)
    {
        using var factory = new PreparationCreationApiFactory();
        await using var seed = factory.Context();
        seed.ServiceVersions.Add(new ServiceVersion(10, 5));
        await seed.SaveChangesAsync();
        var stale = new StaleVersionRead();
        await using var db = new PaperTrailContext(new DbContextOptionsBuilder<PaperTrailContext>()
            .UseSqlite(seed.Database.GetConnectionString()).AddInterceptors(stale).Options);

        await new ServiceVersionRepository(db).UpdateIfNewerAsync(10, incoming, default);

        Assert.True(stale.Used);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(expected, (await seed.ServiceVersions.AsNoTracking().SingleAsync()).Version);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"eventId\":\"invalid\",\"serviceId\":1,\"version\":2,\"occurredAt\":\"2026-11-18T12:00:00Z\"}")]
    [InlineData("{\"eventId\":\"00000000-0000-0000-0000-000000000000\",\"serviceId\":1,\"version\":2,\"occurredAt\":\"2026-11-18T12:00:00Z\"}")]
    [InlineData("{\"eventId\":\"2df5b740-2a3f-498e-9a69-e31f0548cc94\",\"serviceId\":1,\"version\":2,\"occurredAt\":\"0001-01-01T00:00:00Z\"}")]
    public async Task InvalidMessagesDoNotWriteAnything(string json)
    {
        using var factory = new PreparationCreationApiFactory();
        await using var db = factory.Context();
        var exception = await Record.ExceptionAsync(() => new CatalogEventHandler(new ServiceVersionRepository(db))
            .HandleAsync(Encoding.UTF8.GetBytes(json), default));
        Assert.True(exception is JsonException or DomainException);
        Assert.Empty(await db.ServiceVersions.ToListAsync());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    [InlineData(1, -2)]
    [InlineData(long.MaxValue, 2)]
    [InlineData(1, long.MaxValue)]
    public async Task InvalidIdentifiersAndVersionsAreRejected(long serviceId, long version)
    {
        using var factory = new PreparationCreationApiFactory();
        await using var db = factory.Context();
        await Assert.ThrowsAsync<DomainException>(() => new CatalogEventHandler(new ServiceVersionRepository(db))
            .HandleAsync(Message(serviceId, version), default));
        Assert.Empty(await db.ServiceVersions.ToListAsync());
    }

    [Fact]
    public async Task OversizedMessagesAreRejectedBeforeWriting()
    {
        using var factory = new PreparationCreationApiFactory();
        await using var db = factory.Context();
        await Assert.ThrowsAsync<JsonException>(() => new CatalogEventHandler(new ServiceVersionRepository(db))
            .HandleAsync(new byte[4097], default));
        Assert.Empty(await db.ServiceVersions.ToListAsync());
    }

    [Fact]
    public async Task NewVersionChangesTheProgressFlagWithoutChangingPersonalItems()
    {
        using var factory = new PreparationCreationApiFactory();
        long preparationId;
        await using (var db = factory.Context())
        {
            var preparation = new Preparation("1000", 1, 2, "Personal preparation");
            db.Preparations.Add(preparation);
            await db.SaveChangesAsync();
            preparationId = preparation.Id;
            var item = new PreparationItem(preparationId, 11, "Personal title", true);
            item.Complete();
            db.PreparationItems.Add(item);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        var before = JsonDocument.Parse(await client.GetStringAsync($"/preparativos/{preparationId}/progresso"));
        Assert.False(before.RootElement.GetProperty("sourceOutdated").GetBoolean());

        await using (var db = factory.Context())
            await new CatalogEventHandler(new ServiceVersionRepository(db)).HandleAsync(Message(1, 3), default);

        var after = JsonDocument.Parse(await client.GetStringAsync($"/preparativos/{preparationId}/progresso"));
        Assert.True(after.RootElement.GetProperty("sourceOutdated").GetBoolean());
        Assert.True(after.RootElement.GetProperty("ready").GetBoolean());
        await using var check = factory.Context();
        Assert.Equal(2, (await check.Preparations.SingleAsync()).SourceVersion);
        var unchanged = await check.PreparationItems.SingleAsync();
        Assert.Equal("Personal title", unchanged.Title);
        Assert.Equal(11, unchanged.RequirementId);
        Assert.True(unchanged.Required);
        Assert.True(unchanged.Completed);
    }

    private sealed class StaleVersionRead : DbCommandInterceptor
    {
        public bool Used { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Used && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("PT_SERVICE_VERSION", StringComparison.Ordinal))
            {
                command.CommandText = command.CommandText.Replace("WHERE", "WHERE 1 = 0 AND", StringComparison.Ordinal);
                Used = true;
            }
            return ValueTask.FromResult(result);
        }
    }
}
