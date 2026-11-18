namespace PaperTrail.Application.Repositories;

public interface IServiceVersionRepository
{
    Task<long?> GetVersionAsync(long serviceId, CancellationToken cancellationToken);
    Task UpdateIfNewerAsync(long serviceId, long version, CancellationToken cancellationToken);
}
