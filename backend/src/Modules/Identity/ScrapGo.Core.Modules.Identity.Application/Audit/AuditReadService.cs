using System.Text.Json;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Audit;

/// <param name="Actor">The acting user's id.</param>
/// <param name="Action">An exact event type, e.g. <c>user.disabled</c>.</param>
/// <param name="Target">The user a change was made to (<c>targetUserId</c> in the metadata).</param>
/// <param name="From">Inclusive.</param>
/// <param name="To">Exclusive; must be after <paramref name="From"/>.</param>
public sealed record ListAuditQuery(
    int? Actor, string? Action, int? Target, DateTimeOffset? From, DateTimeOffset? To, int? Page, int? PageSize);

/// <summary>One audit row for the admin portal.</summary>
/// <param name="ActorEmail">The acting user's email, when the actor is a user who still exists.</param>
public sealed record AuditEntryDto(
    int Id,
    DateTimeOffset EventTime,
    string ActorType,
    int? ActorUserId,
    string? ActorEmail,
    string Action,
    JsonElement? Metadata);

/// <summary>
/// <c>GET /api/admin/audit</c>: the shared audit log, read-only. The route
/// requires <c>audit.view</c>, checked before this runs.
/// </summary>
public sealed class AuditReadService(IAuditLogReader auditLog, IUserRepository users)
{
    /// <returns>Null for a bad page or a date range that ends before it starts (400 <c>invalid_request</c>).</returns>
    public async Task<PagedResult<AuditEntryDto>?> ListAsync(ListAuditQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        if (!page.IsValid
            || query is { From: { } from, To: { } to } && to <= from
            || query.Actor is <= 0
            || query.Target is <= 0)
        {
            return null;
        }

        var entries = await auditLog.ListAsync(
            new AuditLogFilter(query.Actor, query.Action, query.Target, query.From, query.To), page, cancellationToken);

        // Audit rows have no foreign keys, so a deleted actor simply has no email.
        var actorIds = entries.Items.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!.Value).Distinct().ToList();
        var emails = await users.GetEmailsAsync(actorIds, cancellationToken);

        return new PagedResult<AuditEntryDto>(
            [
                .. entries.Items.Select(e => new AuditEntryDto(
                    e.Id,
                    e.EventTime,
                    e.ActorType,
                    e.ActorUserId,
                    e.ActorUserId is { } id ? emails.GetValueOrDefault(id) : null,
                    e.EventType,
                    e.Metadata)),
            ],
            entries.Page,
            entries.PageSize,
            entries.TotalCount);
    }
}
