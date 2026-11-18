using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PaperTrail.API.BackgroundServices;
using PaperTrail.API.Configuration.CatalogEvents;
using PaperTrail.API.Extensions;

namespace PaperTrail.API.Tests.CatalogEvents;

public sealed class CatalogEventConfigurationTests
{
    [Fact]
    public void DisabledConsumerDoesNotRequireBrokerSettings()
    {
        var services = new ServiceCollection();
        services.AddPaperTrailCatalogEvents(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<CatalogEventConsumer>());
        Assert.Empty(provider.GetServices<IHostedService>());
    }

    [Theory]
    [InlineData("", "queue", 5)]
    [InlineData("https://localhost", "queue", 5)]
    [InlineData("amqp://localhost", "", 5)]
    [InlineData("amqp://localhost", "queue", 0)]
    [InlineData("amqp://localhost", "queue", 61)]
    public void EnabledConsumerRejectsInvalidConfiguration(string address, string queue, int retryDelay)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CatalogEvents:Enabled"] = "true",
            ["CatalogEvents:Uri"] = address,
            ["CatalogEvents:Queue"] = queue,
            ["CatalogEvents:RetryDelaySeconds"] = retryDelay.ToString()
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPaperTrailCatalogEvents(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CatalogEventOptions>>().Value);
    }
}
