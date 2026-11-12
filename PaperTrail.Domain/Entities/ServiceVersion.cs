using PaperTrail.Domain.Common;

namespace PaperTrail.Domain.Entities;

public sealed class ServiceVersion
{
    public long ServiceId { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ServiceVersion() { }

    public ServiceVersion(long serviceId, long version)
    {
        ServiceId = DomainRules.PositiveNumber(serviceId, nameof(ServiceId));
        Version = DomainRules.PositiveNumber(version, nameof(Version));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
