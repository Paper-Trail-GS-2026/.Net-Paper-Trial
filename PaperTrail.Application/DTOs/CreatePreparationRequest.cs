using System.Text.Json.Serialization;

namespace PaperTrail.Application.DTOs;

public record CreatePreparationRequest(
    [property: JsonRequired] long ServiceId,
    [property: JsonRequired] string Title);

public record PreparationCreationResponse(int StatusCode, string Json, long PreparationId);
