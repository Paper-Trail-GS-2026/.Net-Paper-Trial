using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Repositories;

public interface IPreparationRepository
{
    Task<(IReadOnlyList<Preparation> Items, int Total)> GetPagedByOwnerAsync(
        string ownerSubject, int page, int pageSize, CancellationToken cancellationToken);

    Task<Preparation?> GetByIdAndOwnerAsync(long id, string ownerSubject, CancellationToken cancellationToken);
}
