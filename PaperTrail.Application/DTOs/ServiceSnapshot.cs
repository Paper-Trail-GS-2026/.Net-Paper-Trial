using System.Text.Json.Serialization;

namespace PaperTrail.Application.DTOs;

public record ServiceSnapshot(
    [property: JsonRequired] long ServiceId,
    [property: JsonRequired] long Version,
    [property: JsonRequired] string Title,
    [property: JsonRequired] IReadOnlyList<RequirementSnapshot> Requirements);

public record RequirementSnapshot(
    [property: JsonRequired] long Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] bool Required,
    [property: JsonRequired] string SourceUrl);
