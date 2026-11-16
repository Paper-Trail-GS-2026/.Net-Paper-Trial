using PaperTrail.Domain.Entities;
using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Domain.Tests;

public sealed class PreparationItemTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NewItemsStartIncomplete(bool required)
    {
        var item = new PreparationItem(101, 11, " Documento ", required);
        Assert.Equal(101, item.PreparationId);
        Assert.Equal(11, item.RequirementId);
        Assert.Equal("Documento", item.Title);
        Assert.Equal(required, item.Required);
        Assert.False(item.Completed);
    }

    [Fact]
    public void ItemMayHaveNoCatalogRequirement()
    {
        var item = new PreparationItem(101, null, "Personal note", false);
        Assert.Null(item.RequirementId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000000000000000000L)]
    public void ReferencesMustBeValidIds(long value)
    {
        Assert.Throws<DomainException>(() => new PreparationItem(value, 11, "Title", true));
        Assert.Throws<DomainException>(() => new PreparationItem(101, value, "Title", true));
    }

    [Fact]
    public void CompleteAndReopenChangeOnlyTheState()
    {
        var item = new PreparationItem(101, 11, "Documento", true);
        item.Complete();
        Assert.True(item.Completed);
        item.Complete();
        Assert.True(item.Completed);
        item.Reopen();
        Assert.False(item.Completed);
        Assert.Equal("Documento", item.Title);
        Assert.True(item.Required);
    }

    [Fact]
    public void UpdateChangesTitleAndRequiredButKeepsLinksAndState()
    {
        var item = new PreparationItem(101, 11, "Documento", true);
        item.Complete();
        item.Update("  Novo título  ", false);
        Assert.Equal("Novo título", item.Title);
        Assert.False(item.Required);
        Assert.True(item.Completed);
        Assert.Equal(101, item.PreparationId);
        Assert.Equal(11, item.RequirementId);
    }

    [Fact]
    public void InvalidUpdateDoesNotChangeTheItem()
    {
        var item = new PreparationItem(101, 11, "Documento", true);
        Assert.Throws<DomainException>(() => item.Update(" ", false));
        Assert.Throws<DomainException>(() => item.Update(new string('x', 201), false));
        Assert.Equal("Documento", item.Title);
        Assert.True(item.Required);
    }

    [Fact]
    public void ItemTitleMustBePresentAndFitTheColumn()
    {
        Assert.Throws<DomainException>(() => new PreparationItem(101, 11, " ", true));
        Assert.Throws<DomainException>(() => new PreparationItem(101, 11, new string('x', 201), true));
    }
}
