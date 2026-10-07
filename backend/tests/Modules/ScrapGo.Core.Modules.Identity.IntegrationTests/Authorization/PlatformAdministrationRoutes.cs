// ORG-APP-MODULE-MODEL: [PlatformAdministration] skips the membership guard, so every endpoint
// carrying it must be protected by a platform-scoped permission instead. Proven on the real route table.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Api.Authorization;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class PlatformAdministrationRoutes(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
{
    [Fact]
    public void Every_platform_administration_endpoint_requires_a_platform_scoped_permission()
    {
        var endpoints = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<PlatformAdministrationAttribute>() is not null)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            var requirements = endpoint.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>()
                .SelectMany(data => data.GetRequirements())
                .OfType<PermissionRequirement>()
                .ToList();

            Assert.True(
                requirements.Count > 0 && requirements.All(r => r.PlatformScope),
                $"{endpoint.RoutePattern.RawText} is [PlatformAdministration] but lacks a platform-scoped [RequirePermission].");
        });
    }

    [Fact]
    public void Every_platform_administration_endpoint_lives_under_api_admin()
    {
        var outside = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<PlatformAdministrationAttribute>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .Where(route => !route.TrimStart('/').StartsWith("api/admin/", StringComparison.Ordinal));

        Assert.Empty(outside);
    }
}
