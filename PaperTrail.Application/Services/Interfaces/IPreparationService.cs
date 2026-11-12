using PaperTrail.Application.DTOs;

namespace PaperTrail.Application.Services.Interfaces;

public interface IPreparationService
{
    Task<PreparationCreationResponse> CreateAsync(string ownerSubject, string key, string bearerToken,
        CreatePreparationRequest request, CancellationToken cancellationToken);

    Task<PagedResponse<PreparationResponse>> GetPagedAsync(
        string ownerSubject, PaginationQuery query, CancellationToken cancellationToken);

    Task<PreparationResponse> GetByIdAsync(long id, string ownerSubject, CancellationToken cancellationToken);
}
