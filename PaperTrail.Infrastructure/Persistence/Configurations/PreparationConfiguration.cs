using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Configurations;

public sealed class PreparationConfiguration : IEntityTypeConfiguration<Preparation>
{
    public void Configure(EntityTypeBuilder<Preparation> builder)
    {
        builder.ToTable("PT_PREPARATION");
        builder.HasKey(p => p.Id).HasName("PK_PT_PREPARATION");
        builder.Property(p => p.Id).HasColumnName("ID").HasColumnType("NUMBER(18)").UseIdentityColumn();
        builder.Property(p => p.OwnerSubject).HasColumnName("OWNER_SUBJECT").HasColumnType("VARCHAR2(128 CHAR)").IsRequired();
        builder.Property(p => p.ServiceId).HasColumnName("SERVICE_ID").HasColumnType("NUMBER(18)");
        builder.Property(p => p.SourceVersion).HasColumnName("SOURCE_VERSION").HasColumnType("NUMBER(18)");
        builder.Property(p => p.Title).HasColumnName("TITLE").HasColumnType("VARCHAR2(160 CHAR)").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("TIMESTAMP WITH TIME ZONE");
        builder.HasIndex(p => new { p.OwnerSubject, p.CreatedAt }).HasDatabaseName("IX_PT_PREPARATION_OWNER");
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PreparationId)
            .HasConstraintName("FK_PT_ITEM_PREPARATION").OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
