using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PaperTrail.API.Extensions;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Services.Interfaces;

namespace PaperTrail.API.Endpoints;

public static class PreparationEndpoints
{
    public static IEndpointRouteBuilder MapPreparationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/preparativos").WithTags("Preparativos")
            .RequireAuthorization(PaperTrailAuthenticationExtensions.CitizenOrManager);

        group.MapGet("/", GetPagedAsync).WithName("GetPreparations").WithSummary("List your preparations")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503);
        group.MapGet("/{id:long}", GetByIdAsync).WithName("GetPreparation").WithSummary("Get one of your preparations")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503);
        return endpoints;
    }

    private static async Task<Ok<PagedResponse<PreparationResponse>>> GetPagedAsync(
        ClaimsPrincipal user, IPreparationService service, CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        var response = await service.GetPagedAsync(user.FindFirst("sub")!.Value,
            new PaginationQuery { Page = page, PageSize = pageSize }, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<PreparationResponse>> GetByIdAsync(
        long id, ClaimsPrincipal user, IPreparationService service, CancellationToken cancellationToken)
    {
        var response = await service.GetByIdAsync(id, user.FindFirst("sub")!.Value, cancellationToken);
        return TypedResults.Ok(response);
    }
}
