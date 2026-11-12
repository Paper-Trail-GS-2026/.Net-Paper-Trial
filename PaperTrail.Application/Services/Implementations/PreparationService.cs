using PaperTrail.Application.DTOs;
using PaperTrail.Application.Exceptions;
using PaperTrail.Application.Repositories;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Domain.Common;

namespace PaperTrail.Application.Services.Implementations;

public sealed class PreparationService(IPreparationRepository preparations) : IPreparationService
{
    public async Task<PagedResponse<PreparationResponse>> GetPagedAsync(
        string ownerSubject, PaginationQuery query, CancellationToken cancellationToken)
    {
        query.Validate();
        DomainRules.RequiredText(ownerSubject, 128, nameof(ownerSubject));
        var (items, total) = await preparations.GetPagedByOwnerAsync(ownerSubject, query.Page, query.PageSize, cancellationToken);
        return new PagedResponse<PreparationResponse>(items.Select(PreparationResponse.FromDomain).ToList(),
            query.Page, query.PageSize, total);
    }

    public async Task<PreparationResponse> GetByIdAsync(long id, string ownerSubject, CancellationToken cancellationToken)
    {
        DomainRules.PositiveNumber(id, nameof(id));
        DomainRules.RequiredText(ownerSubject, 128, nameof(ownerSubject));
        var preparation = await preparations.GetByIdAndOwnerAsync(id, ownerSubject, cancellationToken)
            ?? throw new ResourceNotFoundException("Preparation not found.");
        return PreparationResponse.FromDomain(preparation);
    }
}
