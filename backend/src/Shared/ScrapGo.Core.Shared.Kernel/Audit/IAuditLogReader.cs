using System.Text.Json;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Shared.Kernel.Audit;

/// <summary>Filters for reading <c>audit.audit_logs</c>. Every filter is optional; omitted means "any".</summary>
/// <param name="ActorUserId">The acting user (<see cref="AuditEvent.UserId"/>).</param>
/// <param name="EventType">Exact event type, e.g. <c>user.disabled</c>.</param>
/// <param name="TargetUserId">The user a change was made to: <c>targetUserId</c> in the row's metadata.</param>
/// <param name="From">Inclusive lower bound on the event time.</param>
/// <param name="To">Exclusive upper bound on the event time.</param>
public sealed record AuditLogFilter(
    int? ActorUserId = null,
    string? EventType = null,
    int? TargetUserId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

/// <summary>One audit row as the reader returns it.</summary>
/// <param name="Metadata">The row's structured detail, or null.</param>
public sealed record AuditLogEntry(
    int Id,
    DateTimeOffset EventTime,
    string ActorType,
    int? ActorUserId,
    string EventType,
    JsonElement? Metadata);

/// <summary>
/// The read-only side of the audit log, over the shared <c>audit</c> schema.
/// Writes stay with <see cref="IAuditLog{TModule}"/>; there is no update or delete.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>Matching rows, newest first. Assumes a valid <see cref="PageRequest"/>.</summary>
    Task<PagedResult<AuditLogEntry>> ListAsync(AuditLogFilter filter, PageRequest page, CancellationToken cancellationToken);
}
