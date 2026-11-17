using PaperTrail.Application.Checklist;
using PaperTrail.Infrastructure.Persistence.Repositories;

namespace PaperTrail.API.Extensions;

public static class ChecklistServiceCollectionExtensions
{
    public static IServiceCollection AddPaperTrailChecklist(this IServiceCollection services)
    {
        services.AddScoped<IChecklistRepository, ChecklistRepository>();
        services.AddScoped<IChecklistService, ChecklistService>();
        return services;
    }
}
