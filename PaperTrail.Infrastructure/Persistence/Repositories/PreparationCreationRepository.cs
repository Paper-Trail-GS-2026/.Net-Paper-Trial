using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Repositories;

public sealed class PreparationCreationRepository(PaperTrailContext context) : IPreparationCreationRepository
{
    public Task<IdempotencyRecord?> FindAsync(string ownerSubject, string key, CancellationToken cancellationToken) =>
        context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.OwnerSubject == ownerSubject
            && r.Operation == IdempotencyRecord.CreatePreparationOperation && r.IdempotencyKey == key, cancellationToken);

    public async Task<IdempotencyRecord> CreateAsync(Preparation preparation, IReadOnlyList<RequirementSnapshot> requirements,
        string key, string payloadHash, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var stored = await FindAsync(preparation.OwnerSubject, key, cancellationToken);
            if (stored is not null) return stored;

            context.Preparations.Add(preparation);
            await context.SaveChangesAsync(cancellationToken);
            context.PreparationItems.AddRange(requirements.Select(r =>
                new PreparationItem(preparation.Id, r.Id, r.Title, r.Required)));
            await context.SaveChangesAsync(cancellationToken);

            var json = JsonSerializer.Serialize(PreparationResponse.FromDomain(preparation), JsonSerializerOptions.Web);
            var record = new IdempotencyRecord(preparation.OwnerSubject, key, payloadHash, json);
            context.IdempotencyRecords.Add(record);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return record;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            context.ChangeTracker.Clear();
            // Another request may have finished the same key while this one was writing.
            var winner = await FindAsync(preparation.OwnerSubject, key, cancellationToken);
            if (winner is not null) return winner;
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
