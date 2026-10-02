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

## Admin Portal — Next Steps & Backend Dependencies

The admin area (`/admin`, `/settings`) is planned but not built. It consumes
only `ScrapGo.Core.Api`; several screens are blocked on backend work listed in
[`../backend/ADMIN-API-GAPS.md`](../backend/ADMIN-API-GAPS.md).

| Frontend feature            | Depends on                                            | Status                                           |
| --------------------------- | ----------------------------------------------------- | ------------------------------------------------ |
| Admin nav + route gate      | `CurrentUserDto.permissions` (extend `/api/users/me`) | Interim: gate on `classification === "Internal"` |
| Organizations list/create   | `GET/POST /api/organizations`                         | Buildable now                                    |
| Organization edit           | `PUT /api/organizations/{id}`                         | Blocked                                          |
| Roles table                 | `GET /api/roles`                                      | Interim: browser-local registry of created roles |
| Role create/edit/delete     | `POST/PUT/DELETE /api/roles`                          | Buildable now                                    |
| Role permission composition | `GET /api/roles/{id}/permissions` (read-back)         | Attach/detach buildable; read-back blocked       |
| Permission catalog          | `GET /api/permissions`                                | Buildable now                                    |
| User administration         | `GET /api/users`, role assignment, enable/disable     | Blocked entirely                                 |
| Settings / profile          | `GET /api/users/me`, linked-provider link             | Buildable now                                    |

Interim workarounds (local role registry, `classification` gate) must be
removed once the corresponding backend endpoints land.
