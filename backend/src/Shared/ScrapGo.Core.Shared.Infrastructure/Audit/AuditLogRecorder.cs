namespace ScrapGo.Core.Shared.Infrastructure.Audit;

/// <summary>
/// Stages an <see cref="AuditLog"/> row on <typeparamref name="TContext"/>,
/// the calling module's own DbContext. It never saves, so the row commits or
/// rolls back with whatever the caller saves next.
/// </summary>
public sealed class AuditLogRecorder<TModule, TContext>(TContext dbContext) : IAuditLog<TModule>
    where TContext : DbContext
{
    public void Record(AuditEvent auditEvent) =>
        dbContext.Set<AuditLog>().Add(new AuditLog
        {
            EventType = auditEvent.EventType,
            UserId = auditEvent.UserId,
            OrganizationId = auditEvent.OrganizationId,
            Metadata = auditEvent.Metadata,
            ActorType = auditEvent.ActorType,
            ServicePrincipalId = auditEvent.ServicePrincipalId,
        });
}
