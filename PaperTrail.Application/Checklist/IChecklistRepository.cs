using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Checklist;

public interface IChecklistRepository
{
    // pendingOnly: required items that are not completed.
    Task<(IReadOnlyList<PreparationItem> Items, int Total)> GetPagedAsync(
        long preparationId, bool pendingOnly, int page, int pageSize, CancellationToken cancellationToken);

    // Returns a tracked item, so changes are saved with SaveAsync.
    Task<PreparationItem?> GetAsync(long preparationId, long itemId, CancellationToken cancellationToken);

    Task AddAsync(PreparationItem item, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(long preparationId, long itemId, CancellationToken cancellationToken);

    Task<(int Total, int Required, int CompletedRequired)> CountAsync(long preparationId, CancellationToken cancellationToken);
}
