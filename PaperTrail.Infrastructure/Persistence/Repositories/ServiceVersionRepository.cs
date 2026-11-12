using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.Repositories;

namespace PaperTrail.Infrastructure.Persistence.Repositories;

public sealed class ServiceVersionRepository(PaperTrailContext context) : IServiceVersionRepository
{
    public Task<long?> GetVersionAsync(long serviceId, CancellationToken cancellationToken) =>
        context.ServiceVersions.AsNoTracking().Where(v => v.ServiceId == serviceId)
            .Select(v => (long?)v.Version).SingleOrDefaultAsync(cancellationToken);
}
