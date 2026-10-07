# GCIP `beforeSignIn` blocking function: Workspace `hd` claim

Platform administration requires a Google Workspace sign-in on every request
(Decision 12 in [`ORG-APP-MODULE-MODEL.md`](../../../ORG-APP-MODULE-MODEL.md)): the
API checks the ID token's `hd` claim against `INTERNAL_HD_ALLOWLIST`.

**GCIP ID tokens don't carry `hd` on their own**, even for Google sign-ins. The
Workspace domain is in Google's token, which GCIP consumes at sign-in. This
function runs on every sign-in. For a Google sign-in from a Workspace account,
it copies Google's verified `hd` into the GCIP token as a session claim.

| Sign-in | `hd` in token | Platform access |
| --- | --- | --- |
| Google, Workspace account on the allowlist | yes | yes |
| Google, other Workspace domain | yes (not on allowlist) | no |
| Google, personal account | no | no |
| Email/password, or any other provider | no | no |

Session claims are set server-side by GCIP and kept across token refreshes. A
client can't add them. Without this function deployed and registered, nobody
has platform access.

## Prerequisites (one-time, Identity Platform console)

1. **Google provider enabled:** Identity Platform → Providers → Add → Google. Note its **Web client ID**; the portal needs it as `VITE_GOOGLE_CLIENT_ID`.
2. **Authorized domains:** Identity Platform → Settings → Security → add the portal's domain(s), and `localhost` for development.
3. **OAuth client origins:** APIs & Services → Credentials → that Web client → Authorized JavaScript origins → add the portal origin(s) (`http://localhost:5173` for development).

## Deploy

```bash
cd backend/infra/gcip-blocking-function
npm install
npm test

gcloud functions deploy beforeSignIn \
  --project=wrigley-cloud-prod \
  --region=us-central1 \
  --no-gen2 \
  --runtime=nodejs20 \
  --trigger-http \
  --allow-unauthenticated \
  --entry-point=beforeSignIn \
  --source=.
```

`--allow-unauthenticated` is required: GCIP calls the function over HTTP, and
`gcip-cloud-functions` verifies GCIP's signed request itself, rejecting
anything else. If an organization policy blocks `allUsers` invokers, ask for an
exception for this function.

## Register with Identity Platform

Identity Platform → **Settings** → **Triggers** → **Before sign in** → choose
`beforeSignIn` → Save.

Session claims only work from **Before sign in**; leave Before account creation
empty for this function.

## Verify

1. Sign in to the portal with "Sign in with Google" using a `@scrapgo.com` account.
2. In the browser dev tools, take the token from `localStorage.authToken` and decode it (for example at jwt.io). It should contain `"hd": "scrapgo.com"` and `firebase.sign_in_provider: "google.com"`.
3. `GET /api/users/me` now shows the platform roles. Platform routes no longer return 403 `workspace_sign_in_required`.

**Use a `@scrapgo.com` account as platform admin.** A personal Google account
(such as `@gmail.com`) has no `hd`, so it never gets platform access. Sign in
once with Google as the Workspace user. That provisions them as Internal. Then run
`bootstrap-platform-admin --uid <their GCIP uid>`.

If the same email already has an email/password account with a verified email,
GCIP may refuse the Google sign-in ("account exists with different
credential") until Google is linked to that account in GCIP.
