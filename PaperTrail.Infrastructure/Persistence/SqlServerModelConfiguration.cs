using Microsoft.EntityFrameworkCore;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence;

internal static class SqlServerModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var preparation = modelBuilder.Entity<Preparation>();
        SqlServerPropertyBuilderExtensions.UseIdentityColumn(preparation.Property(p => p.Id).HasColumnType("bigint"));
        preparation.Property(p => p.OwnerSubject).HasColumnType("nvarchar(128)");
        preparation.Property(p => p.ServiceId).HasColumnType("bigint");
        preparation.Property(p => p.SourceVersion).HasColumnType("bigint");
        preparation.Property(p => p.Title).HasColumnType("nvarchar(160)");
        preparation.Property(p => p.CreatedAt).HasColumnType("datetimeoffset(7)");

        var item = modelBuilder.Entity<PreparationItem>();
        item.ToTable("PT_PREPARATION_ITEM", table => table.UseSqlOutputClause(false));
        SqlServerPropertyBuilderExtensions.UseIdentityColumn(item.Property(i => i.Id).HasColumnType("bigint"));
        item.Property(i => i.PreparationId).HasColumnType("bigint");
        item.Property(i => i.RequirementId).HasColumnType("bigint");
        item.Property(i => i.Title).HasColumnType("nvarchar(200)");
        item.Property(i => i.Required).HasColumnType("int");
        item.Property(i => i.Completed).HasColumnType("int");

        var version = modelBuilder.Entity<ServiceVersion>();
        version.Property(v => v.ServiceId).HasColumnType("bigint");
        version.Property(v => v.Version).HasColumnType("bigint");
        version.Property(v => v.UpdatedAt).HasColumnType("datetimeoffset(7)");

        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.Property(r => r.OwnerSubject).HasColumnType("nvarchar(128)");
        idempotency.Property(r => r.Operation).HasColumnType("nvarchar(64)");
        idempotency.Property(r => r.IdempotencyKey).HasColumnType("nvarchar(128)").UseCollation("Latin1_General_100_BIN2");
        idempotency.Property(r => r.PayloadHash).HasColumnType("varchar(64)");
        idempotency.Property(r => r.ResponseStatus).HasColumnType("int");
        idempotency.Property(r => r.ResponseJson).HasColumnType("nvarchar(max)");
        idempotency.Property(r => r.CreatedAt).HasColumnType("datetimeoffset(7)");
    }
}
