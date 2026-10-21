using System.Text.Json;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Shared.Infrastructure.Audit;

/// <summary>
/// <see cref="IAuditLogReader"/> over <see cref="AuditDbContext"/>: untracked
/// reads only, so no module's DbContext or schema is involved.
/// </summary>
public sealed class AuditLogReader(AuditDbContext dbContext) : IAuditLogReader
{
    public async Task<PagedResult<AuditLogEntry>> ListAsync(AuditLogFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var logs = dbContext.AuditLogs.AsNoTracking();

        if (filter.ActorUserId is { } actor)
        {
            logs = logs.Where(a => a.UserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            var eventType = filter.EventType.Trim();
            logs = logs.Where(a => a.EventType == eventType);
        }

        if (filter.TargetUserId is { } target)
        {
            // jsonb containment (@>), so a number in the metadata matches exactly.
            var containment = JsonSerializer.Serialize(new { targetUserId = target });
            logs = logs.Where(a => a.Metadata != null && EF.Functions.JsonContains(a.Metadata, containment));
        }

        if (filter.From is { } from)
        {
            logs = logs.Where(a => a.EventTime >= from);
        }

        if (filter.To is { } to)
        {
            logs = logs.Where(a => a.EventTime < to);
        }

        var totalCount = await logs.CountAsync(cancellationToken);
        var rows = await logs
            .OrderByDescending(a => a.EventTime)
            .ThenByDescending(a => a.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AuditLogEntry>.Create([.. rows.Select(ToEntry)], page, totalCount);
    }

    private static AuditLogEntry ToEntry(AuditLog row) =>
        new(
            row.Id,
            row.EventTime,
            row.ActorType.ToString(),
            row.UserId,
            row.EventType,
            row.Metadata is null ? null : JsonDocument.Parse(row.Metadata).RootElement.Clone());
}
