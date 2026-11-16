using PaperTrail.Application.DTOs;
using PaperTrail.Application.Exceptions;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Checklist;
using PaperTrail.Domain.Common;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Checklist;

public sealed class ChecklistService(IPreparationRepository preparations, IChecklistRepository items,
    IServiceVersionRepository versions) : IChecklistService
{
    public async Task<PagedResponse<ItemResponse>> GetItemsAsync(long preparationId, string ownerSubject, bool pendingOnly,
        PaginationQuery query, CancellationToken cancellationToken)
    {
        query.Validate();
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        var (found, total) = await items.GetPagedAsync(preparationId, pendingOnly, query.Page, query.PageSize, cancellationToken);
        return new PagedResponse<ItemResponse>(found.Select(ItemResponse.FromDomain).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<ItemResponse> GetItemAsync(long preparationId, long itemId, string ownerSubject,
        CancellationToken cancellationToken)
    {
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        return ItemResponse.FromDomain(await FindItemAsync(preparationId, itemId, cancellationToken));
    }

    public async Task<ItemResponse> AddItemAsync(long preparationId, string ownerSubject, ItemRequest request,
        CancellationToken cancellationToken)
    {
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        var item = new PreparationItem(preparationId, null, request.Title, request.Required);
        await items.AddAsync(item, cancellationToken);
        return ItemResponse.FromDomain(item);
    }

    public async Task<ItemResponse> UpdateItemAsync(long preparationId, long itemId, string ownerSubject, ItemRequest request,
        CancellationToken cancellationToken)
    {
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        var item = await FindItemAsync(preparationId, itemId, cancellationToken);
        item.Update(request.Title, request.Required);
        await items.SaveAsync(cancellationToken);
        return ItemResponse.FromDomain(item);
    }

    public async Task<ItemResponse> SetStateAsync(long preparationId, long itemId, string ownerSubject,
        ItemStateRequest request, CancellationToken cancellationToken)
    {
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        var item = await FindItemAsync(preparationId, itemId, cancellationToken);
        if (request.Completed) item.Complete();
        else item.Reopen();
        await items.SaveAsync(cancellationToken);
        return ItemResponse.FromDomain(item);
    }

    public async Task DeleteItemAsync(long preparationId, long itemId, string ownerSubject, CancellationToken cancellationToken)
    {
        await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        DomainRules.PositiveNumber(itemId, nameof(itemId));
        if (!await items.DeleteAsync(preparationId, itemId, cancellationToken))
            throw new ResourceNotFoundException("Item not found.");
    }

    public async Task<ChecklistProgress> GetProgressAsync(long preparationId, string ownerSubject,
        CancellationToken cancellationToken)
    {
        var preparation = await FindPreparationAsync(preparationId, ownerSubject, cancellationToken);
        var (total, required, completedRequired) = await items.CountAsync(preparationId, cancellationToken);
        var currentVersion = await versions.GetVersionAsync(preparation.ServiceId, cancellationToken);
        return ChecklistProgress.Calculate(total, required, completedRequired, preparation.SourceVersion, currentVersion);
    }

    // A preparation of another citizen looks the same as a missing one (404).
    private async Task<Preparation> FindPreparationAsync(long preparationId, string ownerSubject, CancellationToken cancellationToken)
    {
        DomainRules.PositiveNumber(preparationId, nameof(preparationId));
        DomainRules.RequiredText(ownerSubject, 128, nameof(ownerSubject));
        return await preparations.GetByIdAndOwnerAsync(preparationId, ownerSubject, cancellationToken)
            ?? throw new ResourceNotFoundException("Preparation not found.");
    }

    private async Task<PreparationItem> FindItemAsync(long preparationId, long itemId, CancellationToken cancellationToken)
    {
        DomainRules.PositiveNumber(itemId, nameof(itemId));
        return await items.GetAsync(preparationId, itemId, cancellationToken)
            ?? throw new ResourceNotFoundException("Item not found.");
    }
}
