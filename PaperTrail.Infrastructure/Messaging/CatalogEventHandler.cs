using System.Text.Json;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Common;
using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Infrastructure.Messaging;

public sealed class CatalogEventHandler(IServiceVersionRepository versions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public Task HandleAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (body.Length > 4096) throw new JsonException("Catalog event exceeds 4096 bytes.");
        var message = JsonSerializer.Deserialize<CatalogEvent>(body.Span, JsonOptions)
            ?? throw new JsonException("Catalog event is required.");
        if (message.EventId == Guid.Empty || message.OccurredAt == default)
            throw new DomainException("EventId and occurredAt are required.");
        DomainRules.PositiveNumber(message.ServiceId, nameof(message.ServiceId));
        DomainRules.PositiveNumber(message.Version, nameof(message.Version));
        return versions.UpdateIfNewerAsync(message.ServiceId, message.Version, cancellationToken);
    }

    private sealed record CatalogEvent(Guid EventId, long ServiceId, long Version, DateTimeOffset OccurredAt);
}
