using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Repositories;

public interface IPreparationRepository
{
    Task<(IReadOnlyList<Preparation> Items, int Total)> GetPagedByOwnerAsync(
        string ownerSubject, int page, int pageSize, CancellationToken cancellationToken);

    Task<Preparation?> GetByIdAndOwnerAsync(long id, string ownerSubject, CancellationToken cancellationToken);

    Task<bool> RenameAsync(Preparation preparation, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(long id, string ownerSubject, CancellationToken cancellationToken);
}
