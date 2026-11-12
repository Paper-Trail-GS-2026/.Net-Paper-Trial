using PaperTrail.API.Endpoints;
using PaperTrail.API.Exceptions;
using PaperTrail.API.Extensions;

namespace PaperTrail.API;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddPaperTrailDbContext();
        builder.Services.AddPaperTrailRepositories();
        builder.Services.AddPaperTrailApplicationServices();
        builder.Services.AddPaperTrailCatalogClient();
        builder.Services.AddPaperTrailAuthentication();
        builder.Services.AddPaperTrailSwagger();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            await Results.Problem(statusCode: response.StatusCode).ExecuteAsync(context.HttpContext);
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPreparationEndpoints();
        app.Run();
    }
}
