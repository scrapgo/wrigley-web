# ScrapGo Downstream AI Guidelines

## Project Context

We are building a unified portal that acts as a proxy for Quickbase.

**Backend Role:** The .NET API is the "Security & Governance" layer. It owns the database of truth for Users, Roles, Permissions, and Query Caching.

- **Frontend Role:** The React PWA is the "User Experience" layer. It only interacts with the .NET API. It must never contain raw Quickbase API credentials.

## Backend Rules (.NET / C#)

- **Domain-Driven-ish:** Keep logic separated into modules: `Identity`, `QuickbaseEngine`, `Suppliers`, `Freight`.

- **Caching:** All Quickbase queries MUST be routed through the `QuickbaseQueryService`. Never query Quickbase directly from a Controller.

- **Security:** Every request must be checked against the `UserContext` (Identity/Permissions) before accessing Quickbase data.

## Frontend Rules (React / TanStack)

- **State Management:** Use `TanStack React Query` for all server-state. Use `TanStack Start` loaders for data fetching.

- **Styling:** Use `Tailwind` + `Shadcn UI`. Keep custom CSS to a minimum.

- **Security:** Do not store sensitive keys in the frontend. All API calls must include the Auth header obtained via the Identity Service.

- **UX:** Mobile-first approach. All interfaces must be touch-friendly.

## Workflow Rules

- When modifying a data entity, update the Database schema (Postgres) if it's an Identity/Governance object, or the Quickbase Schema if it's an operational object.

- Always maintain clear separation between the "Proxy" logic (handling legacy Quickbase data) and "Core" logic (Identity/Governance).
