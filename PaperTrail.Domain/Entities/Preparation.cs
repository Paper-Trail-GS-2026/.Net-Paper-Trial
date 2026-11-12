using PaperTrail.Domain.Common;

namespace PaperTrail.Domain.Entities;

public sealed class Preparation : BaseEntity
{
    private readonly List<PreparationItem> _items = [];

    public string OwnerSubject { get; private set; } = string.Empty;
    public long ServiceId { get; private set; }
    public long SourceVersion { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<PreparationItem> Items => _items;

    private Preparation() { }

    public Preparation(string ownerSubject, long serviceId, long sourceVersion, string title)
    {
        OwnerSubject = DomainRules.RequiredText(ownerSubject, 128, nameof(OwnerSubject));
        ServiceId = DomainRules.PositiveNumber(serviceId, nameof(ServiceId));
        SourceVersion = DomainRules.PositiveNumber(sourceVersion, nameof(SourceVersion));
        Title = DomainRules.RequiredText(title, 160, nameof(Title));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string title)
    {
        Title = DomainRules.RequiredText(title, 160, nameof(Title));
    }
}
