using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence.Configurations;

public sealed class ServiceVersionConfiguration : IEntityTypeConfiguration<ServiceVersion>
{
    public void Configure(EntityTypeBuilder<ServiceVersion> builder)
    {
        builder.ToTable("PT_SERVICE_VERSION");
        builder.HasKey(v => v.ServiceId).HasName("PK_PT_SERVICE_VERSION");
        builder.Property(v => v.ServiceId).HasColumnName("SERVICE_ID").HasColumnType("NUMBER(18)").ValueGeneratedNever();
        builder.Property(v => v.Version).HasColumnName("VERSION").HasColumnType("NUMBER(18)");
        builder.Property(v => v.UpdatedAt).HasColumnName("UPDATED_AT").HasColumnType("TIMESTAMP WITH TIME ZONE");
    }
}
