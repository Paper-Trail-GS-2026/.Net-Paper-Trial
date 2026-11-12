using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaperTrail.Domain.Common;

namespace PaperTrail.API.Extensions;

public static class PaperTrailAuthenticationExtensions
{
    public const string CitizenOrManager = "CitizenOrManager";

    public static IServiceCollection AddPaperTrailAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((options, configuration) =>
        {
            var publicKeyPath = configuration["Jwt:PublicKeyPath"];
            if (string.IsNullOrWhiteSpace(publicKeyPath))
                throw new InvalidOperationException("Configure Jwt:PublicKeyPath with the Java public key file.");
            var pem = File.ReadAllText(publicKeyPath);
            if (!pem.Contains("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
                || pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                throw new InvalidOperationException("Jwt:PublicKeyPath must contain only a public RSA key.");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
                throw new InvalidOperationException("The RSA public key must have at least 2048 bits.");
            var key = new RsaSecurityKey(rsa.ExportParameters(false));
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = "papertrail",
                ValidAudience = "papertrail-api",
                IssuerSigningKey = key,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "roles"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var subject = context.Principal?.FindFirst("sub")?.Value;
                    if (!long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                        || id is <= 0 or > DomainRules.MaxNumber
                        || subject != id.ToString(CultureInfo.InvariantCulture))
                        context.Fail("Invalid subject.");
                    return Task.CompletedTask;
                }
            };
        }).ValidateOnStart();
        services.AddAuthorization(options => options.AddPolicy(CitizenOrManager,
            policy => policy.RequireAuthenticatedUser().RequireRole("CITIZEN", "MANAGER")));
        return services;
    }
}
