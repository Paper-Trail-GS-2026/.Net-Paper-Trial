using System.Text.Json.Serialization;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Checklist;

public record ItemRequest([property: JsonRequired] string Title, [property: JsonRequired] bool Required);

public record ItemStateRequest([property: JsonRequired] bool Completed);

public record ItemResponse(long Id, long PreparationId, long? RequirementId, string Title, bool Required, bool Completed)
{
    public static ItemResponse FromDomain(PreparationItem item) =>
        new(item.Id, item.PreparationId, item.RequirementId, item.Title, item.Required, item.Completed);
}
