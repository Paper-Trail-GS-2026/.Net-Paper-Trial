using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PaperTrail.Domain.Entities;
using PaperTrail.Infrastructure.Persistence;

namespace PaperTrail.API.Tests.Persistence;

public sealed class OracleMappingTests
{
    [Fact]
    public void MappingUsesTheExistingTablesAndNumericFlags()
    {
        using var context = Context();
        var preparation = context.Model.FindEntityType(typeof(Preparation))!;
        var item = context.Model.FindEntityType(typeof(PreparationItem))!;
        var version = context.Model.FindEntityType(typeof(ServiceVersion))!;
        Assert.Equal("PT_PREPARATION", preparation.GetTableName());
        Assert.Equal("PT_PREPARATION_ITEM", item.GetTableName());
        Assert.Equal("PT_SERVICE_VERSION", version.GetTableName());
        Assert.Equal("NUMBER(18)", preparation.FindProperty(nameof(Preparation.Id))!.GetColumnType());
        Assert.Equal("TIMESTAMP WITH TIME ZONE", preparation.FindProperty(nameof(Preparation.CreatedAt))!.GetColumnType());
        Assert.Equal(1, item.FindProperty(nameof(PreparationItem.Required))!.GetTypeMapping().Converter!.ConvertToProvider(true));
        Assert.Equal(0, item.FindProperty(nameof(PreparationItem.Completed))!.GetTypeMapping().Converter!.ConvertToProvider(false));
        Assert.Equal(DeleteBehavior.Restrict, item.GetForeignKeys().Single().DeleteBehavior);
        Assert.Empty(version.GetForeignKeys());
    }

    [Fact]
    public void OwnedQueryCanBeTranslatedForOracle19WithoutConnecting()
    {
        using var context = Context();
        var sql = context.Preparations.Where(p => p.OwnerSubject == "1000").OrderBy(p => p.Id).Skip(20).Take(20).ToQueryString();
        Assert.Contains("PT_PREPARATION", sql);
        Assert.Contains("OWNER_SUBJECT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("PT_USER", sql);
    }

    private static PaperTrailContext Context() => new(new DbContextOptionsBuilder<PaperTrailContext>()
        .UseOracle("User Id=test;Password=test;Data Source=localhost/test",
            oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
}
