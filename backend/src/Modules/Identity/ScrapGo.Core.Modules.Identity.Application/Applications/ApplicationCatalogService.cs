using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Applications;

public enum CatalogChangeOutcome
{
    Changed,

    /// <summary>Already in that status. Nothing was written.</summary>
    AlreadyInState,

    /// <summary>Not a known status name.</summary>
    InvalidRequest,

    NotFound,
}

/// <summary>
/// The application catalog. Its definitions live in code
/// (<see cref="ApplicationCatalog"/>, Decision 2); at runtime platform
/// administrators may only retire or reactivate an application or module.
/// </summary>
/// <remarks>
/// Retiring is a platform-wide kill switch: every grant to the application or
/// module stops resolving in every organization, and nothing is deleted. The
/// write routes need <c>Catalog.Manage</c> at platform scope.
/// </remarks>
public sealed class ApplicationCatalogService(
    IAuthorizationQueries queries,
    IApplicationRepository applications,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<CatalogApplicationDto>> ListAsync(CancellationToken cancellationToken) =>
        queries.ListCatalogAsync(cancellationToken);

    public async Task<CatalogChangeOutcome> ChangeApplicationStatusAsync(
        string actorUid, int applicationId, string? status, CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var target))
        {
            return CatalogChangeOutcome.InvalidRequest;
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await applications.FindCatalogApplicationAsync(applicationId, ct) is not { } application)
            {
                return CatalogChangeOutcome.NotFound;
            }

            if (application.Status == target)
            {
                return CatalogChangeOutcome.AlreadyInState;
            }

            var before = application.Status;
            application.ChangeStatus(target, timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.CatalogApplicationChanged,
                UserId: actorUserId,
                Metadata: JsonSerializer.Serialize(new { applicationId, before = before.ToString(), after = target.ToString() })));
            await unitOfWork.SaveChangesAsync(ct);

            return CatalogChangeOutcome.Changed;
        }, cancellationToken);
    }

    public async Task<CatalogChangeOutcome> ChangeModuleStatusAsync(
        string actorUid, int applicationId, int moduleId, string? status, CancellationToken cancellationToken)
    {
        if (!TryParseStatus(status, out var target))
        {
            return CatalogChangeOutcome.InvalidRequest;
        }

        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await applications.FindCatalogModuleAsync(applicationId, moduleId, ct) is not { } module)
            {
                return CatalogChangeOutcome.NotFound;
            }

            if (module.Status == target)
            {
                return CatalogChangeOutcome.AlreadyInState;
            }

            var before = module.Status;
            module.ChangeStatus(target, timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.CatalogModuleChanged,
                UserId: actorUserId,
                Metadata: JsonSerializer.Serialize(new { applicationId, moduleId, before = before.ToString(), after = target.ToString() })));
            await unitOfWork.SaveChangesAsync(ct);

            return CatalogChangeOutcome.Changed;
        }, cancellationToken);
    }

    /// <summary>Names only ("Active", "retired"); numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParseStatus(string? value, out CatalogStatus status)
    {
        status = CatalogStatus.Active;

        return !string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsDigit)
            && Enum.TryParse(value, ignoreCase: true, out status);
    }
}
