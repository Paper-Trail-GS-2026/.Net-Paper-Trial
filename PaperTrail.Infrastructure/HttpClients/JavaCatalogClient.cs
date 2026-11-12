using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PaperTrail.Application.DTOs;
using PaperTrail.Application.Exceptions;
using PaperTrail.Application.Services.Interfaces;
using PaperTrail.Domain.Common;

namespace PaperTrail.Infrastructure.HttpClients;

public sealed class JavaCatalogClient(HttpClient client) : IJavaCatalogClient
{
    public async Task<ServiceSnapshot> GetSnapshotAsync(long serviceId, string bearerToken, CancellationToken cancellationToken)
    {
        DomainRules.PositiveNumber(serviceId, nameof(serviceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"services/{serviceId}/requirements-snapshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ResourceNotFoundException("Service not found.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Sign in again.");
            if (!response.IsSuccessStatusCode)
                throw new CatalogUnavailableException();

            var snapshot = await response.Content.ReadFromJsonAsync<ServiceSnapshot>(cancellationToken);
            ValidateSnapshot(snapshot, serviceId);
            return snapshot!;
        }
        catch (HttpRequestException exception)
        {
            throw new CatalogUnavailableException(exception);
        }
        catch (JsonException exception)
        {
            throw new CatalogUnavailableException(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException(exception);
        }
    }

    private static void ValidateSnapshot(ServiceSnapshot? snapshot, long serviceId)
    {
        if (snapshot is null || snapshot.ServiceId != serviceId || snapshot.Version is <= 0 or > DomainRules.MaxNumber
            || string.IsNullOrWhiteSpace(snapshot.Title) || snapshot.Title.Length > 160 || snapshot.Requirements is null)
            throw new CatalogUnavailableException();

        var ids = new HashSet<long>();
        foreach (var requirement in snapshot.Requirements)
        {
            if (requirement is null || requirement.Id is <= 0 or > DomainRules.MaxNumber || !ids.Add(requirement.Id)
                || string.IsNullOrWhiteSpace(requirement.Title) || requirement.Title.Length > 200
                || string.IsNullOrWhiteSpace(requirement.SourceUrl) || requirement.SourceUrl.Length > 2048
                || !Uri.TryCreate(requirement.SourceUrl, UriKind.Absolute, out var source)
                || (source.Scheme != Uri.UriSchemeHttps && source.Scheme != Uri.UriSchemeHttp))
                throw new CatalogUnavailableException();
        }
    }
}
