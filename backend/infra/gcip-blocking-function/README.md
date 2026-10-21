# GCIP `beforeSignIn` blocking function: Workspace `hd` claim

`POST /api/session` creates an Internal user only for a verified Google sign-in
whose ID token carries an `hd` on `INTERNAL_HD_ALLOWLIST`
([`docs/ACCESS-MODEL.md`](../../../docs/ACCESS-MODEL.md), section 7).

**GCIP ID tokens don't carry `hd` on their own**, even for Google sign-ins. The
Workspace domain is in Google's token, which GCIP consumes at sign-in. This
function runs on every sign-in. For a Google sign-in from a Workspace account,
it copies Google's verified `hd` into the GCIP token as a session claim.

| Sign-in | `hd` in token | Created as Internal |
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
3. `POST /api/session` creates them as Internal with `scrapgo_user`, and `GET /api/me` shows it.

**Use a `@scrapgo.com` account as Site Admin.** A personal Google account
(such as `@gmail.com`) has no `hd`, so it is never created Internal. Sign in
once with Google as the Workspace user. That provisions them as Internal. Then run
`bootstrap-site-admin --uid <their GCIP uid>`.

If the same email already has an email/password account with a verified email,
GCIP may refuse the Google sign-in ("account exists with different
credential") until Google is linked to that account in GCIP.
