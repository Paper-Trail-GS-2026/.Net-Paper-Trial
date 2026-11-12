using PaperTrail.Application.DTOs;

namespace PaperTrail.Application.Services.Interfaces;

public interface IJavaCatalogClient
{
    Task<ServiceSnapshot> GetSnapshotAsync(long serviceId, string bearerToken, CancellationToken cancellationToken);
}
