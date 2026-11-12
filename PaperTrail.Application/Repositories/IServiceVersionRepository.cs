namespace PaperTrail.Application.Repositories;

public interface IServiceVersionRepository
{
    Task<long?> GetVersionAsync(long serviceId, CancellationToken cancellationToken);
}
