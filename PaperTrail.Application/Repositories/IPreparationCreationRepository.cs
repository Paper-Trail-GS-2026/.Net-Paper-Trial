using PaperTrail.Application.DTOs;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Application.Repositories;

public interface IPreparationCreationRepository
{
    Task<IdempotencyRecord?> FindAsync(string ownerSubject, string key, CancellationToken cancellationToken);

    Task<IdempotencyRecord> CreateAsync(Preparation preparation, IReadOnlyList<RequirementSnapshot> requirements,
        string key, string payloadHash, CancellationToken cancellationToken);
}
