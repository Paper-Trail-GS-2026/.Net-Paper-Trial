namespace PaperTrail.Domain.Checklist;

public sealed record ChecklistProgress(
    int TotalItems, int RequiredItems, int PendingRequiredItems, decimal CompletionPercent, bool Ready, bool SourceOutdated)
{
    // Only required items decide progress. Without required items the checklist is never ready.
    public static ChecklistProgress Calculate(int totalItems, int requiredItems, int completedRequiredItems,
        long sourceVersion, long? currentVersion)
    {
        var pending = requiredItems - completedRequiredItems;
        var percent = requiredItems == 0 ? 0m : Math.Round(100m * completedRequiredItems / requiredItems, 2);
        var outdated = currentVersion is not null && currentVersion > sourceVersion;
        return new ChecklistProgress(totalItems, requiredItems, pending, percent, requiredItems > 0 && pending == 0, outdated);
    }
}
