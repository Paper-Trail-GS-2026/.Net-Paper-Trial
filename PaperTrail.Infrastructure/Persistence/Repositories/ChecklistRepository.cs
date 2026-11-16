using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.Checklist;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Repositories;

public sealed class ChecklistRepository(PaperTrailContext context) : IChecklistRepository
{
    public async Task<(IReadOnlyList<PreparationItem> Items, int Total)> GetPagedAsync(
        long preparationId, bool pendingOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.PreparationItems.AsNoTracking().Where(i => i.PreparationId == preparationId);
        if (pendingOnly) query = query.Where(i => i.Required && !i.Completed);
        var total = await query.CountAsync(cancellationToken);
        var found = await query.OrderBy(i => i.Id).Skip(checked((page - 1) * pageSize)).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (found, total);
    }

    public Task<PreparationItem?> GetAsync(long preparationId, long itemId, CancellationToken cancellationToken) =>
        context.PreparationItems.SingleOrDefaultAsync(i => i.Id == itemId && i.PreparationId == preparationId, cancellationToken);

    public async Task AddAsync(PreparationItem item, CancellationToken cancellationToken)
    {
        context.PreparationItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(long preparationId, long itemId, CancellationToken cancellationToken) =>
        await context.PreparationItems.Where(i => i.Id == itemId && i.PreparationId == preparationId)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public async Task<(int Total, int Required, int CompletedRequired)> CountAsync(
        long preparationId, CancellationToken cancellationToken)
    {
        var items = await context.PreparationItems.AsNoTracking().Where(i => i.PreparationId == preparationId)
            .Select(i => new { i.Required, i.Completed }).ToListAsync(cancellationToken);
        return (items.Count, items.Count(i => i.Required), items.Count(i => i.Required && i.Completed));
    }
}
