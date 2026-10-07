namespace ScrapGo.Core.Modules.Identity.Domain.Applications;

/// <summary>One module of a catalog application, as defined in code.</summary>
/// <param name="Id">Stable, explicit id; never reused.</param>
/// <param name="Key">URL- and code-safe key, unique within the application (e.g. <c>invoices</c>).</param>
/// <param name="Permissions">
/// The module's permission names, globally unique as <c>{App}.{Module}.{Action}</c>
/// (Decision 1). Each one must also be appended to <c>Permissions.All</c>,
/// which gives it its positional catalog id.
/// </param>
public sealed record ModuleDefinition(int Id, string Key, string Name, IReadOnlyList<string> Permissions);

/// <summary>
/// A platform-defined application role ("template"): organization_id null,
/// application_id set, read-only. Seeded by migration SQL by name.
/// </summary>
/// <param name="Permissions">This application's module permissions, plus <c>Application.ManageAccess</c> for its administrator.</param>
public sealed record RoleTemplateDefinition(string Name, string Description, IReadOnlyList<string> Permissions);

/// <param name="Id">Stable, explicit id; never reused.</param>
/// <param name="Key">URL- and code-safe key, globally unique (e.g. <c>price-optimizer</c>).</param>
/// <param name="RoleTemplates">At least "{App} Administrator" (<c>Application.ManageAccess</c> plus every module permission).</param>
public sealed record ApplicationDefinition(
    int Id, string Key, string Name, IReadOnlyList<ModuleDefinition> Modules, IReadOnlyList<RoleTemplateDefinition> RoleTemplates);

/// <summary>
/// The application catalog: the platform's applications, their modules, and
/// each module's permissions. It is a compile-time contract, like
/// <see cref="Authorization.Permissions"/>, because <c>[RequirePermission]</c>
/// names permissions at compile time. The rows are seeded by migration
/// (Decision 2), and admin endpoints change only their status.
/// </summary>
/// <remarks>
/// <para>
/// To add an application (see <see cref="DownstreamApplication"/> and CATALOG-AND-ADMIN-GUIDE.md):
/// <list type="number">
/// <item>Define it in its own file with fresh ids (never reused), its modules,
/// and its role templates, then add it to <see cref="All"/>.</item>
/// <item>Append every module permission to <c>Permissions.All</c> (never reorder).</item>
/// <item>Run <c>dotnet ef migrations add</c>: the application, module and
/// permission rows are generated from this file.</item>
/// <item>Add the role templates to that migration as SQL (see
/// <c>AddDownstreamApplication</c>): role ids are database-generated.</item>
/// </list>
/// </para>
/// <para>
/// Nothing here is granted to anyone: no organization gets the application
/// until a platform administrator assigns it, and no user gets access until
/// an application administrator grants it.
/// </para>
/// </remarks>
public static class ApplicationCatalog
{
    public static readonly IReadOnlyList<ApplicationDefinition> All =
    [
        DownstreamApplication.Definition,
    ];

    /// <summary>The module that owns a permission, or null for an identity/administration permission.</summary>
    public static int? ModuleIdOf(string permissionName) =>
        All.SelectMany(a => a.Modules)
            .FirstOrDefault(m => m.Permissions.Contains(permissionName, StringComparer.Ordinal))
            ?.Id;
}
