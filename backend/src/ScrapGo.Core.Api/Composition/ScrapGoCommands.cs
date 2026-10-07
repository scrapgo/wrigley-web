using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Api.Composition;

/// <summary>
/// One-shot operator commands that run against the fully composed host
/// (configuration, user-secrets, DbContexts) instead of serving HTTP.
/// </summary>
/// <remarks>
/// <c>dotnet run --project src/ScrapGo.Core.Api -- bootstrap-platform-admin --uid &lt;GCIP uid&gt;</c>
/// grants the first PlatformAdministrator. This is deliberately not an
/// HTTP endpoint: it needs direct access to the target database's
/// configuration, which is the trust boundary.
/// </remarks>
public static class ScrapGoCommands
{
    public const string BootstrapPlatformAdmin = "bootstrap-platform-admin";

    /// <summary>Runs the command named in <paramref name="args"/>, or else the web server.</summary>
    public static async Task RunScrapGoAsync(this WebApplication app, string[] args)
    {
        if (args is [BootstrapPlatformAdmin, ..])
        {
            Environment.ExitCode = await BootstrapPlatformAdminAsync(app, args[1..]);
            return;
        }

        app.UseScrapGoPipeline();
        await app.RunAsync();
    }

    private static async Task<int> BootstrapPlatformAdminAsync(WebApplication app, string[] args)
    {
        if (args is not ["--uid", var uid] || string.IsNullOrWhiteSpace(uid))
        {
            await Console.Error.WriteLineAsync($"Usage: {BootstrapPlatformAdmin} --uid <GCIP uid>");
            return 2;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<BootstrapPlatformAdministratorHandler>();

        var outcome = await handler.HandleAsync(new BootstrapPlatformAdministratorCommand(uid), CancellationToken.None);

        var (message, exitCode) = outcome switch
        {
            BootstrapPlatformAdministratorOutcome.Granted =>
                ($"Granted: {uid} is now PlatformAdministrator.", 0),
            BootstrapPlatformAdministratorOutcome.AlreadyGranted =>
                ($"AlreadyGranted: {uid} already holds PlatformAdministrator. Nothing changed.", 0),
            BootstrapPlatformAdministratorOutcome.UserNotProvisioned =>
                ($"UserNotProvisioned: no user for {uid}. Sign in and call GET /api/users/me once, then retry.", 1),
            BootstrapPlatformAdministratorOutcome.ExternalUserNotAllowed =>
                ($"ExternalUserNotAllowed: {uid} is an external user; platform roles are for internal (Google Workspace) users only.", 1),
            BootstrapPlatformAdministratorOutcome.AnotherAdministratorExists =>
                ("AnotherAdministratorExists: a different user is already PlatformAdministrator. Bootstrap is one-time.", 1),
            _ => throw new InvalidOperationException($"Unhandled {nameof(BootstrapPlatformAdministratorOutcome)}: {outcome}."),
        };

        await (exitCode == 0 ? Console.Out : Console.Error).WriteLineAsync(message);
        return exitCode;
    }
}
