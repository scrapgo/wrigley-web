// Pure logic for the beforeSignIn blocking function, kept separate so it can be
// tested without the gcip-cloud-functions runtime.

/**
 * Session claims to add to the GCIP ID token for this sign-in.
 *
 * Only a Google sign-in carries Google's verified `hd` (the Workspace domain)
 * in its profile. Copying it into the token lets the API check it against
 * INTERNAL_HD_ALLOWLIST on every request. Every other sign-in method (and a
 * personal Google account, which has no hd) gets no claim, so the API never
 * grants platform access to it.
 */
function sessionClaimsFor(context) {
  const info = context && context.additionalUserInfo;
  const hd = info && info.providerId === 'google.com' && info.profile && info.profile.hd;

  return typeof hd === 'string' && hd.length > 0 ? { hd: hd.toLowerCase() } : {};
}

module.exports = { sessionClaimsFor };
