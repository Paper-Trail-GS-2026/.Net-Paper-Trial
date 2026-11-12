using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PaperTrail.API.Extensions;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Domain.Exceptions;

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
        group.MapPost("/", CreateAsync).WithName("CreatePreparation").WithSummary("Create a preparation from the Java snapshot")
            .Produces<PreparationResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePreparationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext context,
        IPreparationService service, CancellationToken cancellationToken)
    {
        if (context.Request.Headers["Idempotency-Key"].Count != 1)
            throw new DomainException("Provide one Idempotency-Key header.");
        var token = await context.GetTokenAsync("access_token")
            ?? throw new UnauthorizedAccessException();
        var result = await service.CreateAsync(context.User.FindFirst("sub")!.Value, idempotencyKey, token, request, cancellationToken);
        context.Response.Headers.Location = $"/preparativos/{result.PreparationId}";
        return Results.Text(result.Json, "application/json", statusCode: result.StatusCode);
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
