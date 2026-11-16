using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PaperTrail.API.Tests.Support;
using PaperTrail.Application.DTOs;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class PreparationManagementTests
{
    private static HttpClient Client(PreparationCreationApiFactory factory, string subject = "1000", string role = "CITIZEN")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject, role));
        return client;
    }

    private static Task<HttpResponseMessage> Create(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/preparativos")
        {
            Content = JsonContent.Create(new { serviceId = 1, title = "Original title" })
        };
        request.Headers.Add("Idempotency-Key", "management-1");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task UpdateChangesOnlyTitleAndPreservesOriginalCreationResponse()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var created = await Create(client);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<PreparationResponse>())!;
        using var response = await client.PutAsJsonAsync(created.Headers.Location,
            new { title = "  New title  ", ownerSubject = "2000", serviceId = 999, sourceVersion = 999 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(original with { Title = "New title" }, await response.Content.ReadFromJsonAsync<PreparationResponse>());
        Assert.Equal(original with { Title = "New title" }, await client.GetFromJsonAsync<PreparationResponse>(created.Headers.Location));
        await using var database = factory.Context();
        var preparation = await database.Preparations.Include(p => p.Items).SingleAsync();
        Assert.Equal("1000", preparation.OwnerSubject);
        Assert.Equal("New title", preparation.Title);
        Assert.Equal(1, preparation.ServiceId);
        Assert.Equal(2, preparation.SourceVersion);
        Assert.Equal(2, preparation.Items.Count);
        Assert.All(preparation.Items, item => Assert.False(item.Completed));
        Assert.Equal(2, factory.AuditCount());
        using var replay = await Create(client);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Single(factory.Catalog.Tokens);
    }

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("MANAGER")]
    public async Task OtherOwnersReceive404AndCannotChangeHeaderOrItems(string role)
    {
        using var factory = new PreparationCreationApiFactory();
        using var owner = Client(factory);
        using var other = Client(factory, "2000", role);
        using var created = await Create(owner);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var update = await other.PutAsJsonAsync(created.Headers.Location, new { title = "Stolen" });
        using var delete = await other.DeleteAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        await using var database = factory.Context();
        Assert.Equal("Original title", (await database.Preparations.SingleAsync()).Title);
        Assert.Equal(2, await database.PreparationItems.CountAsync());
        Assert.Equal(2, factory.AuditCount());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidTitleReturns400WithoutChangingPreparation(string? title)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var created = await Create(client);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var response = await client.PutAsJsonAsync(created.Headers.Location, new { title });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Original title", (await client.GetFromJsonAsync<PreparationResponse>(created.Headers.Location))!.Title);
    }

    [Fact]
    public async Task MissingAndOversizedTitleReturn400AndMaximumLengthIsAccepted()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var created = await Create(client);
        using var missing = await client.PutAsJsonAsync(created.Headers.Location, new { });
        using var oversized = await client.PutAsJsonAsync(created.Headers.Location, new { title = new string('x', 161) });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        using var maximum = await client.PutAsJsonAsync(created.Headers.Location, new { title = new string('x', 160) });
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(1000000000000000000, HttpStatusCode.BadRequest)]
    [InlineData(999, HttpStatusCode.NotFound)]
    public async Task InvalidOrMissingIdIsRejected(long id, HttpStatusCode expected)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var update = await client.PutAsJsonAsync($"/preparativos/{id}", new { title = "New title" });
        using var delete = await client.DeleteAsync($"/preparativos/{id}");
        Assert.Equal(expected, update.StatusCode);
        Assert.Equal(expected, delete.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("OTHER", HttpStatusCode.Forbidden)]
    public async Task WritesRequireAuthenticationAndAnAllowedRole(string? role, HttpStatusCode expected)
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = role is null ? factory.CreateClient() : Client(factory, role: role);
        using var update = await client.PutAsJsonAsync("/preparativos/1", new { title = "New title" });
        using var delete = await client.DeleteAsync("/preparativos/1");
        Assert.Equal(expected, update.StatusCode);
        Assert.Equal(expected, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteRemovesOnlyOwnedHeaderAndItemsAndKeepsAuditAndIdempotency()
    {
        using var factory = new PreparationCreationApiFactory();
        using var owner = Client(factory);
        using var other = Client(factory, "2000");
        using var created = await Create(owner);
        using var unrelated = await Create(other);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Created, unrelated.StatusCode);
        using var deleted = await owner.DeleteAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync(unrelated.Headers.Location)).StatusCode);
        await using var database = factory.Context();
        Assert.Equal("2000", (await database.Preparations.SingleAsync()).OwnerSubject);
        Assert.Equal(2, await database.PreparationItems.CountAsync());
        Assert.Equal(6, factory.AuditCount());
        Assert.Equal(2, await database.IdempotencyRecords.CountAsync());
        using var replay = await Create(owner);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, await database.Preparations.CountAsync());
        Assert.Equal(2, factory.Catalog.Tokens.Count);
    }

    [Fact]
    public async Task ParentDeleteFailureRollsBackItemsAndDeletionAudit()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        using var created = await Create(client);
        factory.DeleteFailures.FailParentDelete = true;
        using var failed = await client.DeleteAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using (var database = factory.Context())
        {
            Assert.Equal(1, await database.Preparations.CountAsync());
            Assert.Equal(2, await database.PreparationItems.CountAsync());
            Assert.Equal(1, await database.IdempotencyRecords.CountAsync());
        }
        Assert.Equal(2, factory.AuditCount());
        factory.DeleteFailures.FailParentDelete = false;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(4, factory.AuditCount());
    }

    [Fact]
    public async Task EmptyPreparationCanBeDeleted()
    {
        using var factory = new PreparationCreationApiFactory();
        factory.Catalog.Fetch = (id, _) => Task.FromResult(new ServiceSnapshot(id, 1, "Empty", []));
        using var client = Client(factory);
        using var created = await Create(client);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        await using var database = factory.Context();
        Assert.Empty(await database.Preparations.ToListAsync());
    }
}
