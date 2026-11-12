using PaperTrail.Application.DTOs;

namespace PaperTrail.Application.Services.Interfaces;

public interface IPreparationService
{
    Task<PagedResponse<PreparationResponse>> GetPagedAsync(
        string ownerSubject, PaginationQuery query, CancellationToken cancellationToken);

    Task<PreparationResponse> GetByIdAsync(long id, string ownerSubject, CancellationToken cancellationToken);
}
