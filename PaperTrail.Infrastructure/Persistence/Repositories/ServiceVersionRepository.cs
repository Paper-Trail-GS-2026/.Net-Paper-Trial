using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Common;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Repositories;

public sealed class ServiceVersionRepository(PaperTrailContext context) : IServiceVersionRepository
{
    public Task<long?> GetVersionAsync(long serviceId, CancellationToken cancellationToken) =>
        context.ServiceVersions.AsNoTracking().Where(v => v.ServiceId == serviceId)
            .Select(v => (long?)v.Version).SingleOrDefaultAsync(cancellationToken);

    public async Task UpdateIfNewerAsync(long serviceId, long version, CancellationToken cancellationToken)
    {
        DomainRules.PositiveNumber(serviceId, nameof(serviceId));
        DomainRules.PositiveNumber(version, nameof(version));
        var current = await GetVersionAsync(serviceId, cancellationToken);
        if (current >= version) return;
        if (current is not null)
        {
            await UpdateAsync(serviceId, version, cancellationToken);
            return;
        }

        var entry = context.ServiceVersions.Add(new ServiceVersion(serviceId, version));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;
            if (!await context.ServiceVersions.AnyAsync(v => v.ServiceId == serviceId, cancellationToken)) throw;
            await UpdateAsync(serviceId, version, cancellationToken);
        }
    }

    private Task<int> UpdateAsync(long serviceId, long version, CancellationToken cancellationToken)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        return context.ServiceVersions.Where(v => v.ServiceId == serviceId && v.Version < version)
            .ExecuteUpdateAsync(update => update.SetProperty(v => v.Version, version)
                .SetProperty(v => v.UpdatedAt, updatedAt), cancellationToken);
    }
}
