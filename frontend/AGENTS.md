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

## Admin Portal — Current Status

The admin area (`/admin`, `/settings`) is now fully implemented leveraging the
complete backend APIs provided by `ScrapGo.Core.Api`.

| Frontend feature            | Depends on                                            | Status                                            |
| --------------------------- | ----------------------------------------------------- | ------------------------------------------------- |
| Admin nav + route gate      | `CurrentUserDto.permissions` (extend `/api/users/me`) | Implemented: real permission-based access control |
| Organizations list/create   | `GET/POST /api/organizations`                         | Implemented                                       |
| Organization detail/rename  | `GET/PUT /api/organizations/{id}`                     | Implemented                                       |
| Organization members        | `GET/POST/DELETE /api/organizations/{id}/members`     | Implemented                                       |
| Roles table                 | `GET /api/organizations/{id}/roles`                   | Implemented: server-side role listing             |
| Role create/edit/delete     | `POST/PUT/DELETE /api/roles`                          | Implemented                                       |
| Role permission composition | `GET /api/roles/{id}/permissions` (read-back)         | Implemented: server-side permission read-back     |
| Permission catalog          | `GET /api/permissions`                                | Implemented                                       |
| User administration         | `GET /api/users`, role assignment, enable/disable     | Implemented: platform user management             |
| Role assignment             | `POST/DELETE /api/users/{id}/roles`                   | Implemented                                       |
| Settings / profile          | `GET /api/users/me`, linked-provider link             | Implemented                                       |

All interim workarounds (local role registry, `classification` gate) have been
removed and replaced with server-side implementations.
