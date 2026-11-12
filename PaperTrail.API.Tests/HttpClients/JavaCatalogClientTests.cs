using System.Net;
using System.Text;
using PaperTrail.Application.Exceptions;
using PaperTrail.Infrastructure.HttpClients;

namespace PaperTrail.API.Tests.HttpClients;

public sealed class JavaCatalogClientTests
{
    private const string Snapshot = """
        {"serviceId":1,"version":2,"title":"Cadastro fictício","requirements":[
          {"id":11,"title":"Documento","required":true,"sourceUrl":"https://example.invalid/1"},
          {"id":12,"title":"Anotação","required":false,"sourceUrl":"https://example.invalid/1"}]}
        """;

    [Fact]
    public async Task SnapshotKeepsIdsVersionAndBooleanFlags()
    {
        using var handler = new StubHandler(() => Json(Snapshot));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080/") };
        var client = new JavaCatalogClient(http);
        var snapshot = await client.GetSnapshotAsync(1, "token-a", CancellationToken.None);
        Assert.Equal(1, snapshot.ServiceId);
        Assert.Equal(2, snapshot.Version);
        Assert.True(snapshot.Requirements[0].Required);
        Assert.False(snapshot.Requirements[1].Required);
        Assert.Equal("/services/1/requirements-snapshot", handler.Paths.Single());
    }

    [Fact]
    public async Task AuthorizationIsSetPerRequestWithoutLeakingBetweenUsers()
    {
        using var handler = new StubHandler(() => Json(Snapshot));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080/") };
        var client = new JavaCatalogClient(http);
        await client.GetSnapshotAsync(1, "token-a", CancellationToken.None);
        await client.GetSnapshotAsync(1, "token-b", CancellationToken.None);
        Assert.Equal(new[] { "Bearer token-a", "Bearer token-b" }, handler.Authorizations);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task RejectedJavaCredentialsRequireANewLogin(int status)
    {
        using var handler = new StubHandler(() => new HttpResponseMessage((HttpStatusCode)status));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.GetSnapshotAsync(1, "token", CancellationToken.None));
    }

    [Fact]
    public async Task MissingJavaServiceReturnsNotFound()
    {
        using var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => client.GetSnapshotAsync(1, "token", CancellationToken.None));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(302)]
    public async Task UpstreamFailuresAreReportedAsUnavailable(int status)
    {
        using var handler = new StubHandler(() => new HttpResponseMessage((HttpStatusCode)status));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => client.GetSnapshotAsync(1, "token", CancellationToken.None));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{invalid-json")]
    [InlineData("{\"serviceId\":2,\"version\":1,\"title\":\"Title\",\"requirements\":[]}")]
    [InlineData("{\"serviceId\":1,\"version\":0,\"title\":\"Title\",\"requirements\":[]}")]
    [InlineData("{\"serviceId\":1,\"version\":1,\"title\":\" \",\"requirements\":[]}")]
    [InlineData("{\"serviceId\":1,\"version\":1,\"title\":\"Title\",\"requirements\":null}")]
    [InlineData("{\"serviceId\":1,\"version\":1,\"title\":\"Title\",\"requirements\":[null]}")]
    [InlineData("{\"serviceId\":1,\"version\":1,\"title\":\"Title\",\"requirements\":[{\"id\":11,\"title\":\"Title\",\"sourceUrl\":\"https://example.invalid/1\"}]}")]
    public async Task InvalidSnapshotsAreNotAccepted(string body)
    {
        using var handler = new StubHandler(() => Json(body));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        var exception = await Assert.ThrowsAsync<CatalogUnavailableException>(
            () => client.GetSnapshotAsync(1, "token", CancellationToken.None));
        Assert.Equal("Catalog service is unavailable.", exception.Message);
    }

    [Fact]
    public async Task DuplicateRequirementIdsAreRejected()
    {
        using var handler = new StubHandler(() => Json(Snapshot.Replace("\"id\":12", "\"id\":11")));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => client.GetSnapshotAsync(1, "token", CancellationToken.None));
    }

    [Fact]
    public async Task NetworkTimeoutIsReportedAsUnavailable()
    {
        using var handler = new StubHandler(() => throw new TaskCanceledException("Timeout"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new JavaCatalogClient(http);
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => client.GetSnapshotAsync(1, "token", CancellationToken.None));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];
        public List<string?> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            Paths.Add(request.RequestUri?.AbsolutePath);
            return Task.FromResult(response());
        }
    }
}
