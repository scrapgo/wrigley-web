using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// <see cref="ICallerSignIn"/> from the validated bearer token of the current
/// HTTP request. The <c>hd</c> claim is set by Google Identity Platform only
/// for Google Workspace sign-ins, and the token is signed, so it can't be forged.
/// </summary>
public sealed class HttpCallerSignIn(IHttpContextAccessor httpContextAccessor, IOptions<InternalHdAllowlistOptions> allowlist)
    : ICallerSignIn
{
    public bool IsInternalWorkspaceSignIn =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
        && allowlist.Value.Contains(user.FindFirst("hd")?.Value);
}
