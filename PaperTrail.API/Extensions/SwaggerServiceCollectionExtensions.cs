using Microsoft.OpenApi.Models;

namespace PaperTrail.API.Extensions;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddPaperTrailSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "PaperTrail API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Use the token returned by the Java login endpoint."
            });
            options.OperationFilter<BearerSecurityOperationFilter>();
        });
        return services;
    }
}
