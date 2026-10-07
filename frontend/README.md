# ScrapGo Portal Web (Frontend)

## Overview

This is the frontend for the ScrapGo Downstream Portal, a mobile-first Progressive Web App (PWA) built with React, TanStack Start, and Tailwind CSS. It serves as the user interface for internal staff, suppliers, and carriers to access ScrapGo's operational workflows.

## Architecture

The frontend communicates exclusively with the `ScrapGo.Core.Api` backend and never interacts directly with Quickbase. All data flows through the secure .NET API layer.

### Tech Stack

- **Framework:** React with TypeScript
- **Routing:** TanStack Router
- **State Management:** TanStack Query
- **Styling:** Tailwind CSS + Shadcn/UI
- **Build Tool:** Vite
- **Deployment:** Docker container on Google Cloud Run

## Getting Started

### Prerequisites

- Node.js (v20+)
- npm (comes with Node.js)

### Installation

1. Navigate to the frontend directory:

   ```bash
   cd frontend
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

### Development

1. Start the development server:

   ```bash
   npm run dev
   ```

2. Open your browser to http://localhost:5173 (Vite picks the next free port if it's busy).

The development server features:

- Hot Module Replacement (HMR) for instant feedback
- `tsr generate` before start, so the route tree is always current
- API proxy to the backend at http://localhost:5141

**Start the backend first.** Run `dotnet run --project src/ScrapGo.Core.Api` in
`backend/` and wait for `Now listening on: http://localhost:5141`. If the
portal loads before that, Vite logs `http proxy error … ECONNREFUSED`; refresh
once the API is up.

### Building for Production

```bash
npm run build
```

This regenerates the route tree, type-checks (`tsc -b`) and creates an
optimized build in `dist/`. A type error fails the build.

### Previewing Production Build

```bash
npm run preview
```

This serves the production build locally for testing.

## Project Structure

```
src/
├── app/                 # Main application code
│   ├── routes/          # File-based routes
│   ├── components/      # Shared UI components
│   ├── lib/             # Utility functions and API clients
│   ├── hooks/           # Custom React hooks
│   └── router.tsx       # Router configuration
├── assets/              # Static assets
└── index.css           # Global styles
```

## Features

### Authentication

Two sign-in methods, both through **Google Cloud Identity Platform (GCIP)** REST
calls (no Firebase SDK):

| Method | How | Notes |
| --- | --- | --- |
| **Sign in with Google** | Google Identity Services (`lib/google-identity.ts`) returns a Google ID token. `apiClient.signInWithGoogle` exchanges it at `accounts:signInWithIdp` | **Required for platform administrators.** The GCIP `beforeSignIn` blocking function adds the Workspace `hd` claim, which the API checks on every platform request. Shown only when `VITE_GOOGLE_CLIENT_ID` is set |
| **Email and password** | `accounts:signInWithPassword` | Fine for organization and application work; never gives platform access |

Either way, the token is checked against **`GET /api/users/me`** before it's
stored. That call also creates the user's record on their first sign-in. A
stored token is re-checked on every app load, and one the backend rejects
(expired, disabled user) is discarded. `useAuth().refreshUser()` re-reads
`/me`, e.g. after accepting an invitation.

If a platform administrator signs in with email and password, `/me` returns
`workspaceSignInRequired: true`. The Access & Roles page then shows a
"Sign in with Google" notice instead of hiding the admin area.

### Environment Variables

Copy `.env.example` to `.env.local` (gitignored) and set:

```
# Leave empty in development to use the Vite dev proxy (see vite.config.ts),
# which forwards /api to http://localhost:5141 and avoids CORS.
VITE_API_BASE_URL=

# Public GCIP web API key (not a secret).
VITE_GCIP_API_KEY=your-gcip-web-api-key
VITE_GCIP_PROJECT_ID=wrigley-cloud-prod

# Web client id of the Google provider in GCIP (Identity Platform > Providers >
# Google). Public. Blank hides "Sign in with Google".
VITE_GOOGLE_CLIENT_ID=

# Optional: limits Google's account chooser to this Workspace domain (UX only).
VITE_GOOGLE_HOSTED_DOMAIN=scrapgo.com
```

Vite reads these only at startup: restart `npm run dev` after changing them.
For Google sign-in to work, the portal's origin must also be:
- an **authorized JavaScript origin** of that OAuth client
- an **authorized domain** in Identity Platform (`localhost` is there by default)

In development the Vite dev server proxies `/api` to the backend at
`http://localhost:5141`, so the browser stays on a single origin and the
backend's CORS allow-list is never exercised.

### Dashboard

- Stats cards showing key metrics
- Recent activity table with loading skeletons
- Responsive layout for mobile and desktop

### Admin portal (Access & Roles)

The admin area is at `/admin`. It's shown to platform admins and to users with
admin permissions in an organization. Gating is in `hooks/useAdminAccess.ts`:
`can`, `canIn`, `isPlatformAdmin`, `isAppAdmin` and `workspaceSignInRequired`.
The API re-checks everything.

| Screen | Who | What |
| --- | --- | --- |
| **Organizations** tab | Platform admins see every organization (search, status filter); others see theirs | New organization (first admin by user id or email; an email of someone who has signed in is used as their id); **Edit** (rename, which regenerates the slug; set administrator); **Applications**; **Deactivate / Reactivate**; **Delete** (deactivated only, confirmed by typing the name); **Manage** (organizations you belong to) |
| Organization **Applications** dialog | Platform admins | Assign or remove applications, enable or disable modules, appoint an application administrator |
| **Applications** tab | Platform admins | The catalog of applications and modules; Retire / Reactivate with `Catalog.Manage` |
| **Roles**, **Permissions**, **Users** tabs | Admins | Organization roles and permission composition; the permission catalog; user administration and role assignment |
| Organization page (`/admin/organizations/$id`, via **Manage**) | Members with admin rights | Rename, members; **Applications** card where application administrators see who has access and grant or revoke roles, optionally until a date |
| **Settings → Accept an invitation** (`/invitations?token=…`) | Anyone invited | Paste the token to join; needs the invited email, verified (Google counts) |

Step-by-step instructions with test data are in
[`../CATALOG-AND-ADMIN-GUIDE.md`](../CATALOG-AND-ADMIN-GUIDE.md). Status and
remaining gaps are in [`ADMIN-FRONTEND-STATUS.md`](ADMIN-FRONTEND-STATUS.md) and
[`ADMIN-FRONTEND-GAPS-v2.md`](ADMIN-FRONTEND-GAPS-v2.md).

API calls live in `lib/admin-api.ts` and React Query hooks in
`hooks/useAdminQueries.ts`. Error `reason` codes map to messages in
`lib/admin-errors.ts`.

### Routing

- File-based routing with TanStack Router; `routeTree.gen.ts` is generated by `tsr generate` (run by `dev` and `build`), so don't edit it.
- An `admin_.` prefix (e.g. `admin_.organizations.$organizationId.tsx`) makes a page a sibling of `/admin` rather than nested in it, because `/admin` has no `<Outlet />`.
- Protected routes redirect to `/login` without a stored token.

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint
- `npm run routes` - Generate route tree

## Browser Support

The application is designed for modern browsers and optimized for mobile devices. It follows a mobile-first approach with responsive design principles.
