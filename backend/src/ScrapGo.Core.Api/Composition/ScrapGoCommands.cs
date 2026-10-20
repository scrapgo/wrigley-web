using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Api.Composition;

/// <summary>
/// One-shot operator commands that run against the fully composed host
/// (configuration, user-secrets, DbContexts) instead of serving HTTP.
/// </summary>
/// <remarks>
/// <c>dotnet run --project src/ScrapGo.Core.Api -- bootstrap-site-admin --uid &lt;GCIP uid&gt;</c>
/// assigns site_admin to an Internal user while no active Site Admin exists.
/// This is deliberately not an HTTP endpoint: it needs direct access to the
/// target database's configuration, which is the trust boundary.
/// </remarks>
public static class ScrapGoCommands
{
    public const string BootstrapSiteAdmin = "bootstrap-site-admin";

    /// <summary>Runs the command named in <paramref name="args"/>, or else the web server.</summary>
    public static async Task RunScrapGoAsync(this WebApplication app, string[] args)
    {
        if (args is [BootstrapSiteAdmin, ..])
        {
            Environment.ExitCode = await BootstrapSiteAdminAsync(app, args[1..]);
            return;
        }

        app.UseScrapGoPipeline();
        await app.RunAsync();
    }

    private static async Task<int> BootstrapSiteAdminAsync(WebApplication app, string[] args)
    {
        if (args is not ["--uid", var uid] || string.IsNullOrWhiteSpace(uid))
        {
            await Console.Error.WriteLineAsync($"Usage: {BootstrapSiteAdmin} --uid <GCIP uid>");
            return 2;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<BootstrapSiteAdminHandler>();

        var outcome = await handler.HandleAsync(new BootstrapSiteAdminCommand(uid), CancellationToken.None);

        var (message, exitCode) = outcome switch
        {
            BootstrapSiteAdminOutcome.Granted =>
                ($"Granted: {uid} is now a Site Admin.", 0),
            BootstrapSiteAdminOutcome.AlreadyGranted =>
                ($"AlreadyGranted: {uid} is already a Site Admin. Nothing changed.", 0),
            BootstrapSiteAdminOutcome.UserNotProvisioned =>
                ($"UserNotProvisioned: no user for {uid}. Sign in once (POST /api/session), then retry.", 1),
            BootstrapSiteAdminOutcome.ExternalUserNotAllowed =>
                ($"ExternalUserNotAllowed: {uid} is an external user; Site Admin is for internal (Google Workspace) users only.", 1),
            BootstrapSiteAdminOutcome.ActiveSiteAdminExists =>
                ("ActiveSiteAdminExists: an active Site Admin already exists. Ask them to assign the role instead.", 1),
            _ => throw new InvalidOperationException($"Unhandled {nameof(BootstrapSiteAdminOutcome)}: {outcome}."),
        };

        await (exitCode == 0 ? Console.Out : Console.Error).WriteLineAsync(message);
        return exitCode;
    }
}
