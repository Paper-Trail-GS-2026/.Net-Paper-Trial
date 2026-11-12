using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PaperTrail.API.Tests.Support;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class PreparationEndpointTests(PaperTrailApiFactory factory) : IClassFixture<PaperTrailApiFactory>
{
    private HttpClient Client(string? token = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("MANAGER")]
    public async Task ListOnlyReturnsTheSignedInUsersPreparations(string role)
    {
        using var client = Client(factory.Token(role: role));
        using var response = await client.GetAsync("/preparativos?page=1&pageSize=1&ownerSubject=2000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(101, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(1, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task DefaultPaginationWorksWithoutQueryParameters()
    {
        using var client = Client(factory.Token());
        using var response = await client.GetAsync("/preparativos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("CITIZEN")]
    [InlineData("MANAGER")]
    public async Task AnotherOwnersPreparationIsHiddenEvenFromManagers(string role)
    {
        using var client = Client(factory.Token(role: role));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/preparativos/101")).StatusCode);
        using var other = await client.GetAsync("/preparativos/102");
        using var missing = await client.GetAsync("/preparativos/999");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var json = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
        Assert.Equal("Preparation not found.", json.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=invalid")]
    public async Task InvalidPaginationReturns400(string query)
    {
        using var client = Client(factory.Token());
        using var response = await client.GetAsync("/preparativos?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000000000000000")]
    public async Task InvalidIdsReturn400(string id)
    {
        using var client = Client(factory.Token());
        using var response = await client.GetAsync("/preparativos/" + id);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrMalformedTokenReturns401()
    {
        using var anonymous = Client();
        using var invalid = Client("invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/preparativos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.GetAsync("/preparativos")).StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("no-expiration")]
    [InlineData("subject")]
    public async Task InvalidSignedClaimsReturn401(string field)
    {
        var token = factory.Token(subject: field == "subject" ? "invalid" : "1000",
            issuer: field == "issuer" ? "other-project" : "papertrail",
            audience: field == "audience" ? "other-api" : "papertrail-api",
            expired: field == "expired", includeExpiration: field != "no-expiration");
        using var client = Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/preparativos")).StatusCode);
    }

    [Fact]
    public async Task TokenSignedByAnotherKeyReturns401()
    {
        using var rsa = RSA.Create(2048);
        using var client = Client(factory.Token(signingKey: rsa));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/preparativos")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutAnAllowedRoleReturns403()
    {
        using var client = Client(factory.Token(role: "OTHER"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/preparativos")).StatusCode);
    }

    [Fact]
    public async Task SwaggerIsPublicAndDescribesTheProtectedEndpoints()
    {
        using var client = Client();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("bearer", json.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString());
        var endpoints = json.RootElement.GetProperty("paths").EnumerateObject().ToList();
        Assert.Equal(2, endpoints.Count);
        Assert.All(endpoints, endpoint => Assert.True(endpoint.Value.GetProperty("get").GetProperty("security").GetArrayLength() > 0));
    }
}
