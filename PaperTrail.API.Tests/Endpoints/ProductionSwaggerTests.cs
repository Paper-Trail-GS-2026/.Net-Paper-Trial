using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using PaperTrail.API.Tests.Support;

namespace PaperTrail.API.Tests.Endpoints;

public sealed class ProductionSwaggerTests
{
    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task ProductionSwaggerNeedsExplicitConfiguration(bool enabled, HttpStatusCode expected)
    {
        using var factory = new ProductionFactory(enabled);
        using var client = factory.CreateClient();
        Assert.Equal(expected, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/preparativos")).StatusCode);
    }

    private sealed class ProductionFactory(bool enabled) : PaperTrailApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Swagger:Enabled"] = enabled.ToString() }));
        }
    }
}
