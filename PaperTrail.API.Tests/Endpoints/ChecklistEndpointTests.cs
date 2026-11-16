using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PaperTrail.API.Tests.Support;
using PaperTrail.Domain.Entities;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class ChecklistEndpointTests
{
    private static HttpClient Client(PreparationCreationApiFactory factory, string? subject = "1000", string role = "CITIZEN")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (subject is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject, role));
        return client;
    }

    // Creates a preparation with the given items: (title, required, completed).
    private static async Task<(long PreparationId, long[] ItemIds)> Seed(PreparationCreationApiFactory factory,
        string owner, params (string Title, bool Required, bool Completed)[] items)
    {
        await using var db = factory.Context();
        var preparation = new Preparation(owner, 1, 2, "Seed");
        db.Preparations.Add(preparation);
        await db.SaveChangesAsync();
        var created = items.Select(i =>
        {
            var item = new PreparationItem(preparation.Id, null, i.Title, i.Required);
            if (i.Completed) item.Complete();
            return item;
        }).ToList();
        db.PreparationItems.AddRange(created);
        await db.SaveChangesAsync();
        return (preparation.Id, created.Select(i => i.Id).ToArray());
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static HttpRequestMessage Patch(string url, bool completed) =>
        new(HttpMethod.Patch, url) { Content = JsonContent.Create(new { completed }) };

    // 401 / 403

    [Fact]
    public async Task EveryEndpointRequiresAToken()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000", ("A", true, false));
        using var client = Client(factory, subject: null);
        var base_ = $"/preparativos/{prep}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{base_}/itens")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{base_}/itens/{items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{base_}/itens", new { title = "x", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{base_}/itens/{items[0]}", new { title = "x", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Patch($"{base_}/itens/{items[0]}/estado", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"{base_}/itens/{items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{base_}/progresso")).StatusCode);
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(expired: true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/preparativos/{prep}/itens")).StatusCode);
    }

    [Fact]
    public async Task UnknownRoleGets403OnEveryEndpoint()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000", ("A", true, false));
        using var client = Client(factory, role: "VISITOR");
        var base_ = $"/preparativos/{prep}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{base_}/itens")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{base_}/itens", new { title = "x", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Patch($"{base_}/itens/{items[0]}/estado", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{base_}/itens/{items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{base_}/progresso")).StatusCode);
    }

    // 404: another citizen, missing preparation, item from another preparation

    [Fact]
    public async Task AnotherCitizensPreparationIs404OnEveryEndpointAndNothingChanges()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "2000", ("A", true, false));
        using var client = Client(factory, "1000");
        var base_ = $"/preparativos/{prep}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{base_}/itens")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{base_}/itens/{items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{base_}/itens", new { title = "x", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{base_}/itens/{items[0]}", new { title = "x", required = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Patch($"{base_}/itens/{items[0]}/estado", true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{base_}/itens/{items[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{base_}/progresso")).StatusCode);

        await using var db = factory.Context();
        var item = await db.PreparationItems.SingleAsync();
        Assert.Equal("A", item.Title);
        Assert.True(item.Required);
        Assert.False(item.Completed);
    }

    [Fact]
    public async Task MissingPreparationIs404()
    {
        using var factory = new PreparationCreationApiFactory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/preparativos/999/itens")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/preparativos/999/progresso")).StatusCode);
    }

    [Fact]
    public async Task ManagerRoleStillOnlySeesItsOwnPreparations()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "2000", ("A", true, false));
        using var client = Client(factory, "1000", "MANAGER");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/preparativos/{prep}/itens")).StatusCode);
    }

    [Fact]
    public async Task ItemOfAnotherPreparationIs404EvenForTheSameOwner()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prepA, itemsA) = await Seed(factory, "1000", ("Item A", true, false));
        var (prepB, _) = await Seed(factory, "1000", ("Item B", true, false));
        using var client = Client(factory);
        var url = $"/preparativos/{prepB}/itens/{itemsA[0]}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(url, new { title = "x", required = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Patch($"{url}/estado", true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url)).StatusCode);

        await using var db = factory.Context();
        var item = await db.PreparationItems.SingleAsync(i => i.Id == itemsA[0]);
        Assert.Equal(prepA, item.PreparationId);
        Assert.Equal("Item A", item.Title);
        Assert.False(item.Completed);
    }

    [Fact]
    public async Task MissingItemIs404()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/preparativos/{prep}/itens/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/preparativos/{prep}/itens/999")).StatusCode);
    }

    // Owner flow

    [Fact]
    public async Task OwnerCanAddReadUpdateCompleteReopenAndDeleteAnItem()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = Client(factory);

        using var created = await client.PostAsJsonAsync($"/preparativos/{prep}/itens", new { title = "  Levar RG  ", required = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await Json(created);
        var id = item.GetProperty("id").GetInt64();
        Assert.Equal("Levar RG", item.GetProperty("title").GetString());
        Assert.False(item.GetProperty("completed").GetBoolean());
        Assert.Equal($"/preparativos/{prep}/itens/{id}", created.Headers.Location!.OriginalString);

        var url = $"/preparativos/{prep}/itens/{id}";
        Assert.Equal("Levar RG", (await Json(await client.GetAsync(url))).GetProperty("title").GetString());

        var updated = await Json(await client.PutAsJsonAsync(url, new { title = "Levar CPF", required = false }));
        Assert.Equal("Levar CPF", updated.GetProperty("title").GetString());
        Assert.False(updated.GetProperty("required").GetBoolean());

        var done = await Json(await client.SendAsync(Patch($"{url}/estado", true)));
        Assert.True(done.GetProperty("completed").GetBoolean());
        var reopened = await Json(await client.SendAsync(Patch($"{url}/estado", false)));
        Assert.False(reopened.GetProperty("completed").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task UpdateDoesNotChangeTheCompletedState()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000", ("A", true, true));
        using var client = Client(factory);
        var updated = await Json(await client.PutAsJsonAsync($"/preparativos/{prep}/itens/{items[0]}", new { title = "B", required = true }));
        Assert.True(updated.GetProperty("completed").GetBoolean());
    }

    // Empty list, pagination, pending filter

    [Fact]
    public async Task EmptyPreparationReturnsAnEmptyList()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = Client(factory);
        using var response = await client.GetAsync($"/preparativos/{prep}/itens");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        Assert.Equal(0, json.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ListIsPaginatedAndPendingFilterReturnsOnlyRequiredNotCompleted()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000",
            ("Pendente obrigatório", true, false), ("Concluído obrigatório", true, true),
            ("Opcional", false, false), ("Outro pendente", true, false));
        using var client = Client(factory);

        var page = await Json(await client.GetAsync($"/preparativos/{prep}/itens?page=2&pageSize=3"));
        Assert.Equal(4, page.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());

        var pending = await Json(await client.GetAsync($"/preparativos/{prep}/itens?pending=true"));
        Assert.Equal(2, pending.GetProperty("totalItems").GetInt32());
        Assert.Equal(new[] { items[0], items[3] },
            pending.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToArray());
    }

    [Fact]
    public async Task ListOnlyReturnsItemsOfTheRequestedPreparation()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prepA, _) = await Seed(factory, "1000", ("A1", true, false), ("A2", true, false));
        await Seed(factory, "1000", ("B1", true, false));
        using var client = Client(factory);
        var json = await Json(await client.GetAsync($"/preparativos/{prepA}/itens"));
        Assert.Equal(2, json.GetProperty("totalItems").GetInt32());
    }

    // 400

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task InvalidPaginationIs400(string query)
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/preparativos/{prep}/itens?{query}")).StatusCode);
    }

    [Fact]
    public async Task InvalidItemDataIs400AndNothingIsSaved()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000", ("A", true, false));
        using var client = Client(factory);
        var url = $"/preparativos/{prep}/itens";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = " ", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = new string('x', 201), required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{url}/{items[0]}", new { title = " ", required = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"{url}/{items[0]}/estado", new { })).StatusCode);

        await using var db = factory.Context();
        var item = await db.PreparationItems.SingleAsync();
        Assert.Equal("A", item.Title);
        Assert.False(item.Completed);
    }

    [Fact]
    public async Task ClientCannotChooseTheLinksOfANewItem()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        var (otherPrep, _) = await Seed(factory, "1000");
        using var client = Client(factory);
        using var response = await client.PostAsJsonAsync($"/preparativos/{prep}/itens",
            new { title = "x", required = true, preparationId = otherPrep, requirementId = 11, completed = true });
        var item = await Json(response);
        Assert.Equal(prep, item.GetProperty("preparationId").GetInt64());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("requirementId").ValueKind);
        Assert.False(item.GetProperty("completed").GetBoolean());
    }

    // Progress

    [Fact]
    public async Task ProgressOfAPreparationWithoutItemsIsNotReady()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000");
        using var client = Client(factory);
        var json = await Json(await client.GetAsync($"/preparativos/{prep}/progresso"));
        Assert.Equal(0, json.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, json.GetProperty("completionPercent").GetDecimal());
        Assert.False(json.GetProperty("ready").GetBoolean());
        Assert.False(json.GetProperty("sourceOutdated").GetBoolean());
    }

    [Fact]
    public async Task ProgressCountsOnlyRequiredItemsToDecideReady()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, items) = await Seed(factory, "1000",
            ("Obrigatório 1", true, true), ("Obrigatório 2", true, false), ("Opcional", false, false));
        using var client = Client(factory);
        var json = await Json(await client.GetAsync($"/preparativos/{prep}/progresso"));
        Assert.Equal(3, json.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, json.GetProperty("requiredItems").GetInt32());
        Assert.Equal(1, json.GetProperty("pendingRequiredItems").GetInt32());
        Assert.Equal(50, json.GetProperty("completionPercent").GetDecimal());
        Assert.False(json.GetProperty("ready").GetBoolean());

        await client.SendAsync(Patch($"/preparativos/{prep}/itens/{items[1]}/estado", true));
        json = await Json(await client.GetAsync($"/preparativos/{prep}/progresso"));
        Assert.Equal(100, json.GetProperty("completionPercent").GetDecimal());
        Assert.True(json.GetProperty("ready").GetBoolean());
    }

    [Fact]
    public async Task ProgressSignalsAnOutdatedSourceWhenTheCatalogVersionIsNewer()
    {
        using var factory = new PreparationCreationApiFactory();
        var (prep, _) = await Seed(factory, "1000", ("A", true, true));
        await using (var db = factory.Context())
        {
            db.ServiceVersions.Add(new ServiceVersion(1, 3));
            await db.SaveChangesAsync();
        }
        using var client = Client(factory);
        var json = await Json(await client.GetAsync($"/preparativos/{prep}/progresso"));
        Assert.True(json.GetProperty("sourceOutdated").GetBoolean());
    }
}
