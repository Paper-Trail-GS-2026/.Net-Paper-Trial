using PaperTrail.Domain.Common;

namespace PaperTrail.Domain.Entities;

public sealed class PreparationItem : BaseEntity
{
    public long PreparationId { get; private set; }
    public long? RequirementId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool Required { get; private set; }
    public bool Completed { get; private set; }

    private PreparationItem() { }

    public PreparationItem(long preparationId, long? requirementId, string title, bool required)
    {
        PreparationId = DomainRules.PositiveNumber(preparationId, nameof(PreparationId));
        RequirementId = requirementId is null ? null : DomainRules.PositiveNumber(requirementId.Value, nameof(RequirementId));
        Title = DomainRules.RequiredText(title, 200, nameof(Title));
        Required = required;
    }
}
