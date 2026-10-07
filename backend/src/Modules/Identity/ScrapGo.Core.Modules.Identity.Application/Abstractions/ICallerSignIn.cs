namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// How the current request's caller signed in, as proven by their token,
/// not by anything stored about them.
/// </summary>
public interface ICallerSignIn
{
    /// <summary>
    /// True only when the current request's token carries an <c>hd</c>
    /// (Google Workspace hosted domain) claim that is on
    /// <c>INTERNAL_HD_ALLOWLIST</c>. False outside a request.
    /// </summary>
    /// <remarks>
    /// Platform-scoped permissions resolve only when this is true, on every
    /// request. Being classified Internal at provisioning isn't enough: a
    /// Workspace user who later signs in through a linked email/password or
    /// social account carries no <c>hd</c>, and gets no platform access for
    /// that session.
    /// </remarks>
    bool IsInternalWorkspaceSignIn { get; }
}
