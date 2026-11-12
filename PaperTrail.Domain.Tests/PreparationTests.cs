using PaperTrail.Domain.Common;
using PaperTrail.Domain.Entities;
using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Domain.Tests;

public sealed class PreparationTests
{
    [Fact]
    public void CreationPreservesOwnershipAndSnapshotVersion()
    {
        var before = DateTimeOffset.UtcNow;
        var preparation = new Preparation("1000", 1, 2, "  Cadastro municipal  ");
        Assert.Equal("1000", preparation.OwnerSubject);
        Assert.Equal(1, preparation.ServiceId);
        Assert.Equal(2, preparation.SourceVersion);
        Assert.Equal("Cadastro municipal", preparation.Title);
        Assert.Equal(TimeSpan.Zero, preparation.CreatedAt.Offset);
        Assert.InRange(preparation.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Empty(preparation.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OwnerAndTitleCannotBeBlank(string? value)
    {
        Assert.Throws<DomainException>(() => new Preparation(value!, 1, 1, "Title"));
        Assert.Throws<DomainException>(() => new Preparation("1000", 1, 1, value!));
    }

    [Fact]
    public void TextMustFitTheStoredFields()
    {
        Assert.Throws<DomainException>(() => new Preparation(new string('x', 129), 1, 1, "Title"));
        Assert.Throws<DomainException>(() => new Preparation("1000", 1, 1, new string('x', 161)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000000000000000000L)]
    public void ServiceAndVersionMustBePositiveAndFit18Digits(long value)
    {
        Assert.Throws<DomainException>(() => new Preparation("1000", value, 1, "Title"));
        Assert.Throws<DomainException>(() => new Preparation("1000", 1, value, "Title"));
    }

    [Fact]
    public void RenameDoesNotChangeOwnershipOrSnapshot()
    {
        var preparation = new Preparation("1000", 1, 2, "Original");
        var createdAt = preparation.CreatedAt;
        preparation.Rename(" Updated ");
        Assert.Equal("Updated", preparation.Title);
        Assert.Equal("1000", preparation.OwnerSubject);
        Assert.Equal(1, preparation.ServiceId);
        Assert.Equal(2, preparation.SourceVersion);
        Assert.Equal(createdAt, preparation.CreatedAt);
        Assert.Throws<DomainException>(() => preparation.Rename(" "));
        Assert.Equal("Updated", preparation.Title);
    }

    [Fact]
    public void LargestValidIdsAreAccepted()
    {
        var preparation = new Preparation("1000", DomainRules.MaxNumber, DomainRules.MaxNumber, "Title");
        Assert.Equal(DomainRules.MaxNumber, preparation.ServiceId);
    }
}
