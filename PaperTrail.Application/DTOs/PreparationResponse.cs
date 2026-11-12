using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.DTOs;

public record PreparationResponse(long Id, long ServiceId, long SourceVersion, string Title, DateTimeOffset CreatedAt)
{
    public static PreparationResponse FromDomain(Preparation preparation) => new(
        preparation.Id, preparation.ServiceId, preparation.SourceVersion, preparation.Title, preparation.CreatedAt);
}
