using PaperTrail.Application.DTOs;
using PaperTrail.Domain.Checklist;

namespace PaperTrail.Application.Checklist;

public interface IChecklistService
{
    Task<PagedResponse<ItemResponse>> GetItemsAsync(long preparationId, string ownerSubject, bool pendingOnly,
        PaginationQuery query, CancellationToken cancellationToken);

    Task<ItemResponse> GetItemAsync(long preparationId, long itemId, string ownerSubject, CancellationToken cancellationToken);

    Task<ItemResponse> AddItemAsync(long preparationId, string ownerSubject, ItemRequest request, CancellationToken cancellationToken);

    Task<ItemResponse> UpdateItemAsync(long preparationId, long itemId, string ownerSubject, ItemRequest request,
        CancellationToken cancellationToken);

    Task<ItemResponse> SetStateAsync(long preparationId, long itemId, string ownerSubject, ItemStateRequest request,
        CancellationToken cancellationToken);

    Task DeleteItemAsync(long preparationId, long itemId, string ownerSubject, CancellationToken cancellationToken);

    Task<ChecklistProgress> GetProgressAsync(long preparationId, string ownerSubject, CancellationToken cancellationToken);
}
