using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;
using ScrapGo.Core.Modules.Identity.Infrastructure.Authorization;
using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;
using ScrapGo.Core.Shared.Infrastructure.Audit;
using ScrapGo.Core.Shared.Infrastructure.Persistence;
using ScrapGo.Core.Shared.Infrastructure.Web;
using ScrapGo.Core.Shared.Kernel.Audit;

namespace ScrapGo.Core.Modules.Identity.Infrastructure;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddGcipAuthentication(services, configuration);

        // An allow-list that is unset or blank is legitimate (every user is
        // External), so unlike the project id there is no fail-fast here.
        var internalHostedDomains = InternalHdAllowlistOptions.Parse(configuration[InternalHdAllowlistOptions.ConfigurationKey]);
        services.Configure<InternalHdAllowlistOptions>(options => options.HostedDomains = internalHostedDomains);

        // Off unless explicitly turned on (Decision 4b: MFA isn't enabled in GCIP yet).
        var requireMfa = configuration.GetValue<bool>(ExternalUserMfaOptions.ConfigurationKey);
        services.Configure<ExternalUserMfaOptions>(options => options.RequireForExternalUsers = requireMfa);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = PostgresDbContextOptions.GetRequiredConnectionString(configuration);

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseModulePostgres(connectionString, IdentityDbContext.Schema));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILinkedCredentialRepository, LinkedCredentialRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IAuthorizationQueries, AuthorizationQueries>();
        services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IAuditLog<IdentityModule>, AuditLogRecorder<IdentityModule, IdentityDbContext>>();

        // Always resolves from Postgres until the Redis cache is migrated.
        // Swapping this registration is the only change that needs.
        services.AddSingleton<IPermissionCache, PassThroughPermissionCache>();
    }

    private static void AddGcipAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Fail fast: a blank project id would start the app with a bearer
        // scheme that can never validate a real token, surfacing later as a
        // confusing flood of 401s.
        var gcipSection = configuration.GetSection(GcipOptions.SectionName);
        if (string.IsNullOrWhiteSpace(gcipSection[nameof(GcipOptions.ProjectId)]))
        {
            throw new InvalidOperationException("Gcip:ProjectId is not configured (set Gcip__ProjectId).");
        }

        services.Configure<GcipOptions>(options =>
        {
            gcipSection.Bind(options);
            options.ProjectId = options.ProjectId.Trim();
        });

        services.AddHttpClient(GcipSigningKeyProvider.HttpClientName);

        // A singleton built by hand rather than via AddHttpClient<TClient, TImpl>,
        // which would hand back a new, cache-less instance per resolution.
        services.AddSingleton<IGcipSigningKeyProvider>(sp => new GcipSigningKeyProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(GcipSigningKeyProvider.HttpClientName),
            sp.GetRequiredService<IOptions<GcipOptions>>()));

        services.AddSingleton<IReauthTokenValidator, GcipIdTokenValidator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured through the options pipeline so the signing-key resolver
        // uses the DI singleton above.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IGcipSigningKeyProvider, IOptions<GcipOptions>>((options, signingKeyProvider, gcipOptions) =>
            {
                // Keep the raw JWT claim names (sub, email, hd, ...). Without
                // this, the handler remaps "email" to the long WS-Federation
                // URI, so User.FindFirst("email") returns null and the caller's
                // email is provisioned as an empty string.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = GcipTokenValidationParametersFactory.Create(gcipOptions.Value, signingKeyProvider);
                options.Events = new JwtBearerEvents { OnChallenge = WriteTokenLifetimeExceededChallengeAsync };
            });
    }

    /// <summary>
    /// Replaces the bare 401 with a ProblemDetails carrying
    /// <c>reason=token_lifetime_exceeded</c>, but only for the 1-hour-cap
    /// failure. Every other failure keeps the default bare 401.
    /// </summary>
    /// <remarks>
    /// OnChallenge, not OnAuthenticationFailed, is the right extension point:
    /// it fires once, at the point the handler is about to write its 401, so
    /// HandleResponse() replaces that write rather than racing it.
    /// </remarks>
    private static Task WriteTokenLifetimeExceededChallengeAsync(JwtBearerChallengeContext context)
    {
        if (context.AuthenticateFailure is not TokenLifetimeExceededException)
        {
            return Task.CompletedTask;
        }

        context.HandleResponse();

        // HandleResponse() also skips the handler's own WWW-Authenticate
        // write, so set it here to stay RFC 6750 §3 compliant.
        context.Response.Headers.WWWAuthenticate =
            "Bearer error=\"invalid_token\", error_description=\"The token lifetime exceeds the 1 hour cap\"";

        return ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
            StatusCodes.Status401Unauthorized,
            "Token lifetime exceeded",
            "The token's lifetime (exp - iat) exceeds the platform's 1-hour cap.",
            TokenLifetimeExceededException.Reason));
    }
}
