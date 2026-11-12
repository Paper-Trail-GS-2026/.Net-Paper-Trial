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
}
