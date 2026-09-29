namespace ScrapGo.Core.Shared.Kernel.Audit;

/// <summary>
/// Single write path for audit rows. <see cref="Record"/> only stages the row
/// on the owning module's DbContext; it never saves. The caller's own
/// <c>SaveChangesAsync</c>/transaction commit persists it, so the audit row
/// commits or rolls back atomically with the change it describes.
/// </summary>
/// <typeparam name="TModule">
/// Marker type of the calling module. Each module registers its own
/// recorder bound to its own DbContext, so the generic argument is what
/// keeps two modules' audit writers apart in DI.
/// </typeparam>
public interface IAuditLog<TModule>
{
    void Record(AuditEvent auditEvent);
}

/// <summary>
/// The fields every audit call site needs. <see cref="UserId"/> is always the
/// actor, never the subject of a change made to someone else's data; that
/// belongs in <see cref="Metadata"/>.
/// </summary>
public sealed record AuditEvent(
    string EventType,
    int? UserId = null,
    int? OrganizationId = null,
    string? Metadata = null,
    AuditActorType ActorType = AuditActorType.Human,
    int? ServicePrincipalId = null);
