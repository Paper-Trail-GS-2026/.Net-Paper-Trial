using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PaperTrail.API.Extensions;
using PaperTrail.Application.Checklist;
using PaperTrail.Application.DTOs;
using PaperTrail.Domain.Checklist;

namespace PaperTrail.API.Endpoints.Checklist;

public static class ChecklistEndpoints
{
    public static IEndpointRouteBuilder MapChecklistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/preparativos/{preparationId:long}").WithTags("Checklist")
            .RequireAuthorization(PaperTrailAuthenticationExtensions.CitizenOrManager);

        group.MapGet("/itens", GetItemsAsync).WithName("GetChecklistItems")
            .WithSummary("List items of your preparation (pending=true: required and not completed)")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/itens/{itemId:long}", GetItemAsync).WithName("GetChecklistItem")
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapPost("/itens", AddItemAsync).WithName("AddChecklistItem")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapPut("/itens/{itemId:long}", UpdateItemAsync).WithName("UpdateChecklistItem")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapPatch("/itens/{itemId:long}/estado", SetStateAsync).WithName("SetChecklistItemState")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapDelete("/itens/{itemId:long}", DeleteItemAsync).WithName("DeleteChecklistItem")
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/progresso", GetProgressAsync).WithName("GetChecklistProgress")
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        return endpoints;
    }

    private static string Owner(ClaimsPrincipal user) => user.FindFirst("sub")!.Value;

    private static async Task<Ok<PagedResponse<ItemResponse>>> GetItemsAsync(long preparationId, ClaimsPrincipal user,
        IChecklistService service, CancellationToken cancellationToken, bool pending = false, int page = 1, int pageSize = 20) =>
        TypedResults.Ok(await service.GetItemsAsync(preparationId, Owner(user), pending,
            new PaginationQuery { Page = page, PageSize = pageSize }, cancellationToken));

    private static async Task<Ok<ItemResponse>> GetItemAsync(long preparationId, long itemId, ClaimsPrincipal user,
        IChecklistService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetItemAsync(preparationId, itemId, Owner(user), cancellationToken));

    private static async Task<Created<ItemResponse>> AddItemAsync(long preparationId, ItemRequest request, ClaimsPrincipal user,
        IChecklistService service, CancellationToken cancellationToken)
    {
        var item = await service.AddItemAsync(preparationId, Owner(user), request, cancellationToken);
        return TypedResults.Created($"/preparativos/{preparationId}/itens/{item.Id}", item);
    }

    private static async Task<Ok<ItemResponse>> UpdateItemAsync(long preparationId, long itemId, ItemRequest request,
        ClaimsPrincipal user, IChecklistService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.UpdateItemAsync(preparationId, itemId, Owner(user), request, cancellationToken));

    private static async Task<Ok<ItemResponse>> SetStateAsync(long preparationId, long itemId, ItemStateRequest request,
        ClaimsPrincipal user, IChecklistService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.SetStateAsync(preparationId, itemId, Owner(user), request, cancellationToken));

    private static async Task<NoContent> DeleteItemAsync(long preparationId, long itemId, ClaimsPrincipal user,
        IChecklistService service, CancellationToken cancellationToken)
    {
        await service.DeleteItemAsync(preparationId, itemId, Owner(user), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ChecklistProgress>> GetProgressAsync(long preparationId, ClaimsPrincipal user,
        IChecklistService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetProgressAsync(preparationId, Owner(user), cancellationToken));
}
