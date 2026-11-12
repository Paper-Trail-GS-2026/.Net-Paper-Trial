using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Repositories;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Domain.Entities;
using PaperTrail.Infrastructure.Persistence;
using PaperTrail.Infrastructure.Persistence.Repositories;

namespace PaperTrail.API.Tests.Support;

public sealed class PreparationCreationApiFactory : PaperTrailApiFactory
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"papertrail-creation-tests-{Guid.NewGuid():N}");
    private readonly string _connectionString;
    private bool _disposed;
    public TestCatalog Catalog { get; } = new();
    public WriteFailureInterceptor Failures { get; } = new();
    public StaleIdempotencyReadInterceptor StaleReads { get; } = new();

    public PreparationCreationApiFactory()
    {
        Directory.CreateDirectory(_directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "preparations.db"),
            Pooling = false,
            DefaultTimeout = 15
        }.ConnectionString;
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE PT_PREPARATION (
              ID INTEGER PRIMARY KEY AUTOINCREMENT, OWNER_SUBJECT TEXT NOT NULL,
              SERVICE_ID INTEGER NOT NULL, SOURCE_VERSION INTEGER NOT NULL, TITLE TEXT NOT NULL, CREATED_AT TEXT NOT NULL);
            CREATE TABLE PT_PREPARATION_ITEM (
              ID INTEGER PRIMARY KEY AUTOINCREMENT, PREPARATION_ID INTEGER NOT NULL REFERENCES PT_PREPARATION(ID),
              REQUIREMENT_ID INTEGER, TITLE TEXT NOT NULL, REQUIRED INTEGER NOT NULL, COMPLETED INTEGER NOT NULL);
            CREATE TABLE PT_IDEMPOTENCY (
              OWNER_SUBJECT TEXT NOT NULL, OPERATION TEXT NOT NULL, IDEMPOTENCY_KEY TEXT NOT NULL,
              PAYLOAD_HASH TEXT NOT NULL, RESPONSE_STATUS INTEGER NOT NULL, RESPONSE_JSON TEXT NOT NULL,
              CREATED_AT TEXT NOT NULL, PRIMARY KEY(OWNER_SUBJECT, OPERATION, IDEMPOTENCY_KEY));
            CREATE TABLE PT_SERVICE_VERSION (SERVICE_ID INTEGER PRIMARY KEY, VERSION INTEGER NOT NULL, UPDATED_AT TEXT NOT NULL);
            CREATE TABLE PT_ITEM_AUDIT (ID INTEGER PRIMARY KEY AUTOINCREMENT, ITEM_ID INTEGER NOT NULL);
            CREATE TRIGGER ITEM_INSERT_AUDIT AFTER INSERT ON PT_PREPARATION_ITEM
            BEGIN INSERT INTO PT_ITEM_AUDIT(ITEM_ID) VALUES(NEW.ID); END;
            """;
        command.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PaperTrailContext>();
            services.AddScoped(_ => new PaperTrailContext(new DbContextOptionsBuilder<PaperTrailContext>()
                .UseSqlite(_connectionString).AddInterceptors(Failures, StaleReads).Options));
            services.RemoveAll<IPreparationRepository>();
            services.AddScoped<IPreparationRepository, PreparationRepository>();
            services.RemoveAll<IJavaCatalogClient>();
            services.AddSingleton<IJavaCatalogClient>(Catalog);
        });
    }

    public PaperTrailContext Context() => new(new DbContextOptionsBuilder<PaperTrailContext>().UseSqlite(_connectionString).Options);

    public int AuditCount()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PT_ITEM_AUDIT";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        if (!disposing) return;
        File.Delete(Path.Combine(_directory, "preparations.db"));
        Directory.Delete(_directory);
    }
}

public sealed class TestCatalog : IJavaCatalogClient
{
    public ConcurrentQueue<string> Tokens { get; } = new();
    public Func<long, CancellationToken, Task<ServiceSnapshot>> Fetch { get; set; } = (id, _) => Task.FromResult(
        new ServiceSnapshot(id, 2, "Catalog title",
            [new RequirementSnapshot(11, "Documento obrigatório", true, "https://example.invalid/1"),
             new RequirementSnapshot(12, "Anotação opcional", false, "https://example.invalid/1")]));

    public Task<ServiceSnapshot> GetSnapshotAsync(long serviceId, string bearerToken, CancellationToken cancellationToken)
    {
        Tokens.Enqueue(bearerToken);
        return Fetch(serviceId, cancellationToken);
    }
}

public sealed class WriteFailureInterceptor : SaveChangesInterceptor
{
    public bool FailIdempotencyWrite { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FailIdempotencyWrite && eventData.Context!.ChangeTracker.Entries<IdempotencyRecord>()
                .Any(e => e.State == EntityState.Added))
            throw new DbUpdateException("Test failure after items were saved.");
        return ValueTask.FromResult(result);
    }
}

public sealed class StaleIdempotencyReadInterceptor : DbCommandInterceptor
{
    public int ReadsToHide { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (ReadsToHide > 0 && command.CommandText.Contains("FROM \"PT_IDEMPOTENCY\"", StringComparison.Ordinal))
        {
            ReadsToHide--;
            // Simulate a read made before another request committed the key.
            command.CommandText = command.CommandText.Replace("WHERE", "WHERE 1 = 0 AND", StringComparison.Ordinal);
        }
        return ValueTask.FromResult(result);
    }
}
