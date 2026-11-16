using System.Text.Json.Serialization;

namespace PaperTrail.Application.DTOs;

public record UpdatePreparationRequest([property: JsonRequired] string Title);
