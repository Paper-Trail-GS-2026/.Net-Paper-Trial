using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PaperTrail.Application.Repositories;
using PaperTrail.Domain.Common;
using PaperTrail.Domain.Entities;

namespace PaperTrail.API.Tests.Support;

public class PaperTrailApiFactory : WebApplicationFactory<Program>
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _keyDirectory = Path.Combine(Path.GetTempPath(), $"papertrail-api-tests-{Guid.NewGuid():N}");
    private bool _disposed;
    public TestPreparationRepository Repository { get; } = new();

    public PaperTrailApiFactory()
    {
        Directory.CreateDirectory(_keyDirectory);
        File.WriteAllText(Path.Combine(_keyDirectory, "public.pem"), _rsa.ExportSubjectPublicKeyInfoPem());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PaperTrailOracle"] = "User Id=test;Password=test;Data Source=localhost/test",
            ["Jwt:PublicKeyPath"] = Path.Combine(_keyDirectory, "public.pem"),
            ["JavaCatalog:BaseUrl"] = "http://localhost:8080/"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPreparationRepository>();
            services.AddSingleton<IPreparationRepository>(Repository);
        });
    }

    public string Token(string subject = "1000", string role = "CITIZEN", string issuer = "papertrail",
        string audience = "papertrail-api", bool expired = false, bool includeExpiration = true, RSA? signingKey = null)
    {
        var payload = new JwtPayload
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = subject,
            ["iat"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
            ["roles"] = new[] { role }
        };
        if (includeExpiration)
            payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(expired ? -5 : 30).ToUnixTimeSeconds();
        var credentials = new SigningCredentials(new RsaSecurityKey(signingKey ?? _rsa), SecurityAlgorithms.RsaSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
        if (!disposing) return;
        _rsa.Dispose();
        File.Delete(Path.Combine(_keyDirectory, "public.pem"));
        Directory.Delete(_keyDirectory);
    }
}

public sealed class TestPreparationRepository : IPreparationRepository
{
    private readonly List<Preparation> _preparations = [Create(101, "1000"), Create(102, "2000"), Create(103, "1000")];

    private static Preparation Create(long id, string owner)
    {
        var preparation = new Preparation(owner, 1, 2, $"Preparation {id}");
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(preparation, id);
        return preparation;
    }

    public Task<(IReadOnlyList<Preparation> Items, int Total)> GetPagedByOwnerAsync(
        string ownerSubject, int page, int pageSize, CancellationToken cancellationToken)
    {
        var owned = _preparations.Where(p => p.OwnerSubject == ownerSubject).OrderBy(p => p.Id).ToList();
        IReadOnlyList<Preparation> items = owned.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, owned.Count));
    }

    public Task<Preparation?> GetByIdAndOwnerAsync(long id, string ownerSubject, CancellationToken cancellationToken) =>
        Task.FromResult(_preparations.SingleOrDefault(p => p.Id == id && p.OwnerSubject == ownerSubject));
}
