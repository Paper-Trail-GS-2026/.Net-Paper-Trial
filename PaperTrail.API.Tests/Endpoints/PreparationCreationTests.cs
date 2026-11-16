using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PaperTrail.API.Tests.Support;
using PaperTrail.Application.Exceptions;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class PreparationCreationTests
{
    private static HttpClient Client(PreparationCreationApiFactory factory, string subject = "1000", string role = "CITIZEN")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject, role));
        return client;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, string? key = "creation-1", string title = "My preparation", long serviceId = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/preparativos")
        {
            Content = JsonContent.Create(new { serviceId, title })
        };
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task CreatesOwnedHeaderAndSnapshotItemsTogether()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var response = await Create(client, title: "  My preparation  ");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        long id = json.RootElement.GetProperty("id").GetInt64();
        Assert.Equal($"/preparativos/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(2, json.RootElement.GetProperty("sourceVersion").GetInt64());
        await using var database = factory.Context();
        var preparation = await database.Preparations.Include(p => p.Items).SingleAsync();
        Assert.Equal("1000", preparation.OwnerSubject);
        Assert.Equal("My preparation", preparation.Title);
        Assert.Equal(2, preparation.Items.Count);
        Assert.Equal(1, preparation.Items.Count(i => i.Required));
        Assert.All(preparation.Items, item => Assert.False(item.Completed));
        Assert.Equal(new long?[] { 11, 12 }, preparation.Items.OrderBy(i => i.RequirementId).Select(i => i.RequirementId));
        Assert.Single(await database.IdempotencyRecords.ToListAsync());
        Assert.Equal(2, factory.AuditCount());
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, factory.Catalog.Tokens.Single());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task ReplayReturnsTheExactOriginalResponseWithoutCallingJavaAgain()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var first = await Create(client);
        factory.Catalog.Fetch = (_, _) => throw new CatalogUnavailableException();
        using var replay = await Create(client, title: " My preparation ");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(first.StatusCode, replay.StatusCode);
        Assert.Equal(first.Headers.Location, replay.Headers.Location);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Single(factory.Catalog.Tokens);
        await using var database = factory.Context();
        Assert.Equal(1, await database.Preparations.CountAsync());
        Assert.Equal(2, await database.PreparationItems.CountAsync());
    }

    [Theory]
    [InlineData("Different title", 1)]
    [InlineData("My preparation", 2)]
    public async Task SameKeyWithDifferentContentReturns409(string title, long serviceId)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Created, (await Create(client)).StatusCode);
        using var response = await Create(client, title: title, serviceId: serviceId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(factory.Catalog.Tokens);
        await using var database = factory.Context();
        Assert.Equal(1, await database.Preparations.CountAsync());
    }

    [Fact]
    public async Task SameKeyIsIndependentForEachOwner()
    {
        using var factory = new PreparationCreationApiFactory();
        using var first = Client(factory);
        using var second = Client(factory, "2000", "MANAGER");
        using var a = await Create(first);
        using var b = await Create(second);
        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        Assert.NotEqual(a.Headers.Location, b.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync(b.Headers.Location)).StatusCode);
        await using var database = factory.Context();
        Assert.Equal(2, await database.IdempotencyRecords.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRequestsCreateOnlyOnePreparation(bool differentContent)
    {
        using var factory = new PreparationCreationApiFactory();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        var snapshot = await factory.Catalog.Fetch(1, CancellationToken.None);
        factory.Catalog.Fetch = async (_, token) =>
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            return snapshot;
        };
        using var first = Client(factory);
        using var second = Client(factory);
        var results = await Task.WhenAll(Create(first), Create(second, title: differentContent ? "Different" : "My preparation"))
            .WaitAsync(TimeSpan.FromSeconds(30));
        using var a = results[0];
        using var b = results[1];
        if (differentContent)
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, a.StatusCode);
            Assert.Equal(HttpStatusCode.Created, b.StatusCode);
            Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());
        }
        await using var database = factory.Context();
        Assert.Equal(1, await database.Preparations.CountAsync());
        Assert.Equal(2, await database.PreparationItems.CountAsync());
        Assert.Equal(1, await database.IdempotencyRecords.CountAsync());
        Assert.Equal(2, factory.AuditCount());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UniqueKeyRaceRollsBackTheLosingHeaderItemsAndAudit(bool differentContent)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var first = await Create(client);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        factory.StaleReads.ReadsToHide = 2;
        using var replay = await Create(client, title: differentContent ? "Different" : "My preparation");
        Assert.Equal(differentContent ? HttpStatusCode.Conflict : HttpStatusCode.Created, replay.StatusCode);
        if (!differentContent) Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(0, factory.StaleReads.ReadsToHide);
        await using var database = factory.Context();
        Assert.Equal(1, await database.Preparations.CountAsync());
        Assert.Equal(2, await database.PreparationItems.CountAsync());
        Assert.Equal(2, factory.AuditCount());
    }

    [Fact]
    public async Task StorageFailureRollsBackAllRowsAndAllowsRetry()
    {
        using var factory = new PreparationCreationApiFactory();
        factory.Failures.FailIdempotencyWrite = true;
        using var client = Client(factory);
        using var failed = await Create(client);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var database = factory.Context())
        {
            Assert.Equal(0, await database.Preparations.CountAsync());
            Assert.Equal(0, await database.PreparationItems.CountAsync());
            Assert.Equal(0, await database.IdempotencyRecords.CountAsync());
        }
        Assert.Equal(0, factory.AuditCount());
        factory.Failures.FailIdempotencyWrite = false;
        Assert.Equal(HttpStatusCode.Created, (await Create(client)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("two,keys")]
    public async Task MissingOrInvalidKeyReturns400(string? key)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, key)).StatusCode);
        Assert.Empty(factory.Catalog.Tokens);
    }

    [Fact]
    public async Task InvalidFieldsAreRejectedBeforeAnyExternalCall()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, serviceId: 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, title: " ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, title: new string('x', 161))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(client, new string('x', 129))).StatusCode);
        Assert.Empty(factory.Catalog.Tokens);
    }

    [Fact]
    public async Task JavaFailureDoesNotReserveTheKeyOrCreateRows()
    {
        using var factory = new PreparationCreationApiFactory();
        factory.Catalog.Fetch = (_, _) => throw new CatalogUnavailableException();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Create(client)).StatusCode);
        await using var database = factory.Context();
        Assert.Equal(0, await database.Preparations.CountAsync());
        Assert.Equal(0, await database.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task SnapshotWithoutRequirementsCreatesAnEmptyPreparation()
    {
        using var factory = new PreparationCreationApiFactory();
        factory.Catalog.Fetch = (id, _) => Task.FromResult(new PaperTrail.Application.DTOs.ServiceSnapshot(id, 3, "Empty", []));
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Created, (await Create(client)).StatusCode);
        await using var database = factory.Context();
        Assert.Equal(1, await database.Preparations.CountAsync());
        Assert.Equal(0, await database.PreparationItems.CountAsync());
    }

    [Fact]
    public async Task CreationRequiresAuthenticationAndAnAllowedRole()
    {
        using var factory = new PreparationCreationApiFactory();
        using var anonymous = factory.CreateClient();
        using var forbidden = Client(factory, role: "OTHER");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Create(anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(forbidden)).StatusCode);
    }

    [Fact]
    public async Task OwnerVersionAndItemsCannotBeChosenThroughTheRequestBody()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/preparativos")
        {
            Content = JsonContent.Create(new { serviceId = 1, title = "Title", ownerSubject = "2000",
                sourceVersion = 999, items = new[] { new { title = "Fake item", completed = true } } })
        };
        request.Headers.Add("Idempotency-Key", "untrusted-fields");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var database = factory.Context();
        var preparation = await database.Preparations.SingleAsync();
        Assert.Equal("1000", preparation.OwnerSubject);
        Assert.Equal(2, preparation.SourceVersion);
        var items = await database.PreparationItems.ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.False(item.Completed));
        Assert.DoesNotContain(items, item => item.Title == "Fake item");
    }
}
