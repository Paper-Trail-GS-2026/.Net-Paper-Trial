using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Exceptions;
using PaperTrail.Application.Repositories;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Domain.Common;
using PaperTrail.Domain.Entities;
using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Application.Services.Implementations;

public sealed class PreparationService(IPreparationRepository preparations,
    IPreparationCreationRepository creation, IJavaCatalogClient catalog) : IPreparationService
{
    public async Task<PreparationCreationResponse> CreateAsync(string ownerSubject, string key, string bearerToken,
        CreatePreparationRequest request, CancellationToken cancellationToken)
    {
        DomainRules.RequiredText(ownerSubject, 128, nameof(ownerSubject));
        DomainRules.PositiveNumber(request.ServiceId, nameof(request.ServiceId));
        var title = DomainRules.RequiredText(request.Title, 160, nameof(request.Title));
        if (string.IsNullOrEmpty(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~' || c == ','))
            throw new DomainException("Idempotency-Key must be a single value of 1 to 128 visible ASCII characters without commas.");

        var normalized = new CreatePreparationRequest(request.ServiceId, title);
        var payload = JsonSerializer.Serialize(normalized, JsonSerializerOptions.Web);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var stored = await creation.FindAsync(ownerSubject, key, cancellationToken);
        if (stored is not null) return CreationResponse(stored, hash);

        var snapshot = await catalog.GetSnapshotAsync(request.ServiceId, bearerToken, cancellationToken);
        var preparation = new Preparation(ownerSubject, snapshot.ServiceId, snapshot.Version, title);
        stored = await creation.CreateAsync(preparation, snapshot.Requirements, key, hash, cancellationToken);
        return CreationResponse(stored, hash);
    }

    private static PreparationCreationResponse CreationResponse(IdempotencyRecord stored, string hash)
    {
        if (!string.Equals(stored.PayloadHash, hash, StringComparison.Ordinal))
            throw new ConflictException("Idempotency-Key was already used with different content.");
        var response = JsonSerializer.Deserialize<PreparationResponse>(stored.ResponseJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid stored preparation response.");
        return new PreparationCreationResponse(stored.ResponseStatus, stored.ResponseJson, response.Id);
    }

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
