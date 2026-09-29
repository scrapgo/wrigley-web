namespace ScrapGo.Core.Shared.Infrastructure.Audit;

/// <summary>
/// An append-only record of a security-relevant event. It lives in the
/// <c>audit</c> schema and has plain id columns only, with no foreign keys
/// into any module's schema: an audit row must outlive, and never be coupled
/// to, the rows it mentions.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    public AuditActorType ActorType { get; set; } = AuditActorType.Human;

    /// <summary>Free-form event discriminator, e.g. <c>"denied_disabled_user"</c>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The acting user; null for system-initiated events.</summary>
    public int? UserId { get; set; }

    /// <summary>Only populated for <see cref="AuditActorType.Service"/> events.</summary>
    public int? ServicePrincipalId { get; set; }

    /// <summary>The organization the event is scoped to; null for platform-scoped events.</summary>
    public int? OrganizationId { get; set; }

    /// <summary>Event-specific structured detail, stored as <c>jsonb</c>.</summary>
    public string? Metadata { get; set; }

    /// <summary>Stamped by the database (<c>DEFAULT now()</c>), never by the app clock.</summary>
    public DateTimeOffset EventTime { get; set; }

    /// <summary>Flipped by the nightly export job once the row is exported.</summary>
    public bool Exported { get; set; }
}
