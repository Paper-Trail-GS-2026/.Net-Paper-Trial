using Microsoft.EntityFrameworkCore;
using PaperTrail.Domain.Entities;
using PaperTrail.Infrastructure.Persistence;

namespace PaperTrail.API.Tests.Persistence;

public sealed class SqlServerMappingTests
{
    [Fact]
    public void SchemaUsesSqlServerTypesAndPreservesCaseSensitiveIdempotencyKeys()
    {
        using var context = Context();
        var sql = context.Database.GenerateCreateScript();
        Assert.Contains("bigint NOT NULL IDENTITY", sql);
        Assert.Contains("datetimeoffset(7)", sql);
        Assert.Contains("nvarchar(max)", sql);
        Assert.Contains("COLLATE Latin1_General_100_BIN2", sql);
        Assert.DoesNotContain("VARCHAR2", sql);
        Assert.DoesNotContain("NUMBER(", sql);
        Assert.DoesNotContain("CLOB", sql);
        var item = context.Model.FindEntityType(typeof(PreparationItem))!;
        Assert.False(item.IsSqlOutputClauseUsed());
        Assert.Equal(DeleteBehavior.Restrict, item.GetForeignKeys().Single().DeleteBehavior);
    }

    [Fact]
    public void OwnedPaginationCanBeTranslatedForSqlServerWithoutConnecting()
    {
        using var context = Context();
        var sql = context.Preparations.Where(p => p.OwnerSubject == "1000").OrderBy(p => p.Id).Skip(20).Take(20).ToQueryString();
        Assert.Contains("[PT_PREPARATION]", sql);
        Assert.Contains("[OWNER_SUBJECT]", sql);
        Assert.Contains("OFFSET", sql);
    }

    private static PaperTrailContext Context() => new(new DbContextOptionsBuilder<PaperTrailContext>()
        .UseSqlServer("Server=localhost;Database=test;User Id=test;Password=test;Encrypt=True").Options);
}
