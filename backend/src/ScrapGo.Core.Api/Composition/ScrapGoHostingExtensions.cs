using Microsoft.OpenApi;

namespace ScrapGo.Core.Api.Composition;

/// <summary>Host-level web concerns (CORS, Swagger, health, the middleware order), kept out of <c>Program.cs</c>.</summary>
public static class ScrapGoHostingExtensions
{
    private const string CorsPolicyName = "default";

    public static WebApplicationBuilder AddScrapGoHosting(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        // An explicit allow-list, never a wildcard. Leaving Cors:AllowedOrigins
        // unset (empty) matches no origin at all, which is the safe default.
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
            options.AddPolicy(CorsPolicyName, policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

        services.AddHealthChecks();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "ScrapGo Core API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a GCIP ID token. Swagger adds the \"Bearer \" prefix itself.",
            });
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });
        });

        // Cloud Run supplies PORT.
        if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
        {
            builder.WebHost.UseUrls($"http://*:{port}");
        }

        return builder;
    }

    /// <summary>The request pipeline. The order is load-bearing; see the comments.</summary>
    public static WebApplication UseScrapGoPipeline(this WebApplication app)
    {
        // First, so it wraps every other middleware, not just endpoints.
        app.UseExceptionHandler();

        // Cloud Run always runs ASPNETCORE_ENVIRONMENT=Production, so Swagger
        // is gated by explicit config rather than IsDevelopment(). Leave it off
        // outside deliberate, temporary verification.
        if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrapGo Core API v1"));
        }

        app.UseHttpsRedirection();

        // Explicit so the order is visible: the Identity gates read the
        // matched route's {organizationId}, so routing must run before them.
        app.UseRouting();

        // Before authentication and authorization, so a preflight gets its
        // response before anything else can short-circuit the pipeline.
        app.UseCors(CorsPolicyName);

        app.UseAuthentication();

        // Need the authenticated principal and the matched route, and must deny
        // before any policy or endpoint runs: the disabled-user gate, then the
        // cross-tenant membership guard.
        app.UseIdentityModule();

        app.UseAuthorization();

        app.MapHealthChecks("/healthz");
        app.MapControllers();

        return app;
    }
}
