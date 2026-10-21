# ScrapGo Portal Web (Frontend)

## Overview

This is the frontend for the ScrapGo Downstream Portal, a mobile-first Progressive Web App (PWA) built with React, TanStack Start, and Tailwind CSS. It serves as the user interface for internal staff, suppliers, and carriers to access ScrapGo's operational workflows.

## Current Status

For detailed implementation status, see [Frontend Implementation Status](FRONTEND-IMPLEMENTATION-STATUS.md).

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

| Method                  | How                                                                                                                                                  | Notes                                                                                                                                                                                                             |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Sign in with Google** | Google Identity Services (`lib/google-identity.ts`) returns a Google ID token. `apiClient.signInWithGoogle` exchanges it at `accounts:signInWithIdp` | How ScrapGo staff sign in: the GCIP `beforeSignIn` blocking function adds the Workspace `hd` claim. External users may use it too. Shown only when `VITE_GOOGLE_CLIENT_ID` is set |
| **Email and password**  | `accounts:signInWithPassword` (and `accounts:signUp` on the invitation page)                                                                       | For external users, who join by invitation                                                                                                                                                                        |

Every sign-in ends with **`POST /api/session`** (with a pending invitation
token, if any), then **`GET /api/me`**, before the session is stored. A 403
`no_access` or `account_disabled` goes to `/no-access`. The session keeps the
GCIP refresh token and renews the ID token before it expires and on any 401
(`lib/api-client.ts`), so users stay signed in past an hour.

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

# Keep blank: external users sign in with Google too, and a hint hides them.
VITE_GOOGLE_HOSTED_DOMAIN=
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

### Access and the admin portal

Access follows [`../docs/ACCESS-MODEL.md`](../docs/ACCESS-MODEL.md). The
caller's permissions and features come from `GET /api/me`, refetched on window
focus, every 5 minutes and after any 403. In code:

- `useAccess()` (`hooks/useAccess.ts`): `can(...anyOf)` and `isOn(feature)`. Check permission keys only, never role names.
- `<Can anyOf={…}>` and the `RequirePermission` route wrapper (`components/access.tsx`), which renders "not found" for a page the caller can't use.
- The menu is one `NAV` list (`lib/nav.ts`), each entry with the permissions that show it.

The admin area is `/admin`, one tab per permission (`?tab=`): **Users**
(`users.view`; changes need `users.manage`), **Invitations** (`users.invite` or
`users.view`), **Roles** (`roles.view`), **Features** (`features.view`) and
**Audit log** (`audit.view`). Invited users accept at `/invite/accept?token=…`.
The API re-checks everything.

API calls live in `lib/admin-api.ts` and React Query hooks in
`hooks/useAdminQueries.ts`. Error `reason` codes map to messages in
`lib/admin-errors.ts`.

### Routing

- File-based routing with TanStack Router; `routeTree.gen.ts` is generated by `tsr generate` (run by `dev` and `build`), so don't edit it.
- An `admin_.` prefix (e.g. `admin_.users.$userId.tsx`) makes a page a sibling of `/admin` rather than nested in it, because `/admin` has no `<Outlet />`.
- Protected routes redirect to `/login` without a stored session, and wrap their page in `RequirePermission`.

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint
- `npm run routes` - Generate route tree

## Browser Support

The portal is designed for modern browsers and optimized for mobile devices. It follows a mobile-first approach with responsive design principles.
