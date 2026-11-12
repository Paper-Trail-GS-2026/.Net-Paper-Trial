using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Configurations;

public sealed class PreparationItemConfiguration : IEntityTypeConfiguration<PreparationItem>
{
    public void Configure(EntityTypeBuilder<PreparationItem> builder)
    {
        builder.ToTable("PT_PREPARATION_ITEM");
        builder.HasKey(i => i.Id).HasName("PK_PT_ITEM");
        builder.Property(i => i.Id).HasColumnName("ID").HasColumnType("NUMBER(18)").UseIdentityColumn();
        builder.Property(i => i.PreparationId).HasColumnName("PREPARATION_ID").HasColumnType("NUMBER(18)");
        builder.Property(i => i.RequirementId).HasColumnName("REQUIREMENT_ID").HasColumnType("NUMBER(18)");
        builder.Property(i => i.Title).HasColumnName("TITLE").HasColumnType("VARCHAR2(200 CHAR)").IsRequired();
        builder.Property(i => i.Required).HasColumnName("REQUIRED").HasColumnType("NUMBER(1)").HasConversion<int>();
        builder.Property(i => i.Completed).HasColumnName("COMPLETED").HasColumnType("NUMBER(1)").HasConversion<int>();
        builder.HasIndex(i => i.PreparationId).HasDatabaseName("IX_PT_ITEM_PREPARATION");
    }
}
