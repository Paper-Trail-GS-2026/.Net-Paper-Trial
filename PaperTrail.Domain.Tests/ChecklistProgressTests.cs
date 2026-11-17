using PaperTrail.Domain.Checklist;

namespace PaperTrail.Domain.Tests;

public sealed class ChecklistProgressTests
{
    [Fact]
    public void WithoutItemsTheChecklistIsNotReadyAndHasZeroPercent()
    {
        var progress = ChecklistProgress.Calculate(0, 0, 0, 2, 2);
        Assert.Equal(0, progress.CompletionPercent);
        Assert.False(progress.Ready);
        Assert.Equal(0, progress.PendingRequiredItems);
    }

    [Fact]
    public void WithOnlyOptionalItemsTheChecklistIsNeverReady()
    {
        var progress = ChecklistProgress.Calculate(3, 0, 0, 2, 2);
        Assert.Equal(3, progress.TotalItems);
        Assert.Equal(0, progress.CompletionPercent);
        Assert.False(progress.Ready);
    }

    [Fact]
    public void PartialProgressUsesOnlyRequiredItems()
    {
        var progress = ChecklistProgress.Calculate(5, 3, 1, 2, 2);
        Assert.Equal(3, progress.RequiredItems);
        Assert.Equal(2, progress.PendingRequiredItems);
        Assert.Equal(33.33m, progress.CompletionPercent);
        Assert.False(progress.Ready);
    }

    [Fact]
    public void AllRequiredItemsCompletedMeansReady()
    {
        var progress = ChecklistProgress.Calculate(5, 2, 2, 2, 2);
        Assert.Equal(100, progress.CompletionPercent);
        Assert.True(progress.Ready);
    }

    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, null, false)]
    public void SourceIsOutdatedOnlyWhenTheKnownVersionIsNewer(int source, int? current, bool expected)
    {
        Assert.Equal(expected, ChecklistProgress.Calculate(1, 1, 1, source, current).SourceOutdated);
    }
}
