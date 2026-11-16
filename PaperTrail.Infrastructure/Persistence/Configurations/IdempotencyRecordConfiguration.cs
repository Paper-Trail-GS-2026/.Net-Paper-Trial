using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("PT_IDEMPOTENCY");
        builder.HasKey(r => new { r.OwnerSubject, r.Operation, r.IdempotencyKey }).HasName("PK_PT_IDEMPOTENCY");
        builder.Property(r => r.OwnerSubject).HasColumnName("OWNER_SUBJECT").HasColumnType("VARCHAR2(128 CHAR)");
        builder.Property(r => r.Operation).HasColumnName("OPERATION").HasColumnType("VARCHAR2(64 CHAR)");
        builder.Property(r => r.IdempotencyKey).HasColumnName("IDEMPOTENCY_KEY").HasColumnType("VARCHAR2(128 CHAR)");
        builder.Property(r => r.PayloadHash).HasColumnName("PAYLOAD_HASH").HasColumnType("VARCHAR2(64 CHAR)").IsRequired();
        builder.Property(r => r.ResponseStatus).HasColumnName("RESPONSE_STATUS").HasColumnType("NUMBER(3)");
        builder.Property(r => r.ResponseJson).HasColumnName("RESPONSE_JSON").HasColumnType("CLOB").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("TIMESTAMP WITH TIME ZONE");
    }
}
