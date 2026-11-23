using System.Text.Json;
using Ims.Api.Infrastructure;

namespace Ims.Api.Contracts;

public class AuditListQuery : ListQuery
{
    public long? ActorId { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    /// <summary>Inclusive first local day, "yyyy-MM-dd".</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>One audit row. <c>HasValues</c> tells the list whether an expandable old/new comparison exists.</summary>
public record AuditRowDto(
    long Id, DateTime CreatedAt, long? ActorId, string Actor, string Action, string EntityType, string EntityId,
    string EntityRepr, string? IpAddress, bool HasValues);

/// <summary>The detail view: the same row plus the old and new values (JSON objects of ONLY the changed properties).</summary>
public record AuditDetailDto(
    long Id, DateTime CreatedAt, long? ActorId, string Actor, string Action, string EntityType, string EntityId,
    string EntityRepr, string? IpAddress, JsonElement? OldValues, JsonElement? NewValues);

public record AuditFilterOptionsDto(
    IReadOnlyList<OptionDto> Actors, IReadOnlyList<string> Actions, IReadOnlyList<string> EntityTypes);
