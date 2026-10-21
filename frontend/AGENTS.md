# ScrapGo Portal Web (Downstream Frontend)

## Architectural Overview

The `ScrapGo.Portal.Web` is a **Mobile-First, Tablet-Focused Progressive Web App (PWA)** built for internal staff, suppliers, and carriers. It is built using **React**, **TanStack Start** (for full-stack routing and server-side rendering/data loading), **TanStack React Query** (for server state management and caching), **Tailwind CSS**, and **Shadcn/UI**.

The frontend communicates **exclusively** with the `ScrapGo.Core.Api`. It has no knowledge of Quickbase, treating the .NET API as its sole backend for identity, administration, and underlying operational data.

## Core Design Principles

1. **Mobile-First & Touch-Optimized:** Designed primarily for field use (suppliers capturing photos/notes, drivers checking loads) with large touch targets and offline-resilient UI states.

2. **Server State Management:** All API calls flow through **TanStack React Query** to ensure optimistic updates, background refetching, and zero redundant network requests.

3. **Type-Safe Routing:** Utilizing **TanStack Start** for file-based routing, type-safe search parameters, and seamless loaders.

4. **Design System:** Consistent, accessible, and modern styling via **Tailwind CSS** and **Shadcn/UI** components.

## Project Structure

- **`/app/routes`**: File-based routes matching the core domains (Admin, Supplier Portal, Freight, Pricing).

- **`/app/features`**: Domain-specific components, hooks, and business logic (e.g., `loads`, `quickbase-admin`, `pricing-dashboard`).

- **`/app/components/ui`**: Base Shadcn/UI primitive components (buttons, dialogs, tables).

- **`/app/api`**: Typed API client layer communicating exclusively with the `ScrapGo.Core.Api`.

## Tech Stack

- **Framework:** TanStack Start (React framework)

- **State/Data Fetching:** TanStack React Query

- **Styling:** Tailwind CSS

- **Components:** Shadcn/UI

- **PWA Capabilities:** Service Workers for offline caching of critical data.

## Access model

The portal follows [`../docs/ACCESS-MODEL.md`](../docs/ACCESS-MODEL.md):

- **Sign-in:** every sign-in ends with `POST /api/session` (with a pending invitation token from `sessionStorage`), then `GET /api/me`. Refusals (`no_access`, `account_disabled`) go to `/no-access`. The session's refresh token keeps users signed in (`lib/api-client.ts`, `hooks/useAuth.ts`).
- **Gating:** use `useAccess()` (`can(...anyOf)`, `isOn(feature)`), `<Can>` and `RequirePermission` (`components/access.tsx`). Check permission keys only, never role names. Menu entries and admin tabs come from the one `NAV` list in `lib/nav.ts`.
- **Admin:** `/admin` tabs (Users, Invitations, Roles, Features, Audit log) and `/admin/users/$userId`, over the `/api/admin/…` endpoints in `lib/admin-api.ts`.
- **Invitations:** invitees accept at `/invite/accept?token=…`.
