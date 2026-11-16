using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Repositories;

public sealed class PreparationRepository(PaperTrailContext context) : IPreparationRepository
{
    public async Task<(IReadOnlyList<Preparation> Items, int Total)> GetPagedByOwnerAsync(
        string ownerSubject, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.Preparations.AsNoTracking().Where(p => p.OwnerSubject == ownerSubject);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Id).Skip(checked((page - 1) * pageSize)).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Preparation?> GetByIdAndOwnerAsync(long id, string ownerSubject, CancellationToken cancellationToken) =>
        context.Preparations.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id && p.OwnerSubject == ownerSubject, cancellationToken);

    public async Task<bool> RenameAsync(Preparation preparation, CancellationToken cancellationToken) =>
        await context.Preparations.Where(p => p.Id == preparation.Id && p.OwnerSubject == preparation.OwnerSubject)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Title, preparation.Title), cancellationToken) == 1;

    public async Task<bool> DeleteAsync(long id, string ownerSubject, CancellationToken cancellationToken)
    {
        var preparation = await context.Preparations.Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == id && p.OwnerSubject == ownerSubject, cancellationToken);
        if (preparation is null) return false;

        context.PreparationItems.RemoveRange(preparation.Items);
        context.Preparations.Remove(preparation);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
