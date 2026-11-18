namespace PaperTrail.API.Configuration.CatalogEvents;

public sealed class CatalogEventOptions
{
    public string Uri { get; set; } = "";
    public string Exchange { get; set; } = "papertrail.catalog";
    public string RoutingKey { get; set; } = "catalog.service.updated";
    public string Queue { get; set; } = "papertrail.dotnet.catalog-version";
    public int RetryDelaySeconds { get; set; } = 5;
}
