# ScrapGo Downstream Portal

## Overview

Downstream is the centralized operational portal for ScrapGo. It acts as an intelligent, secure bridge between disparate systems (primarily **Quickbase**) and our stakeholders (Suppliers, Carriers, and Internal Staff).

This platform consolidates identity management, pricing transparency, and operational workflow into a mobile-first, PWA-based portal.

## Current Status

For detailed implementation status, see:
- [Backend Implementation Status](backend/BACKEND-IMPLEMENTATION-STATUS.md)
- [Frontend Implementation Status](frontend/FRONTEND-IMPLEMENTATION-STATUS.md)
- [Overall Project Status](PROJECT-STATUS.md)

## Architecture

- **Backend:** .NET 10 / PostgreSQL (Modular Monolith): `ScrapGo.Core.Api`

- **Frontend:** React / TanStack Start / Tailwind / Shadcn UI: `ScrapGo.Portal.Web`

- **Integration:** Quickbase (System of Record)

- **Hosting:** Google Cloud: Cloud Run, Cloud SQL (Postgres 16), Secret Manager, Identity Platform

The frontend talks only to the backend API; the backend is the only component that talks to Quickbase.

## Getting Started

### Prerequisites

- .NET 10 SDK (10.0.101 or later)

- Google Cloud SDK (`gcloud`) and the Cloud SQL Auth Proxy: the API runs against Cloud SQL, with no local database

- Docker Desktop: only for the backend test suite and container builds

- Node.js (v20+): for the frontend

### Running Locally

1. **Backend API.** Full setup, including the one-time `gcloud` sign-in and user-secrets, is in [`backend/README.md`](backend/README.md). Once set up:

   ```bash
   # terminal 1: tunnel to Cloud SQL
   cloud-sql-proxy <project>:<region>:<instance> --port 5434

   # terminal 2: the API
   cd backend
   dotnet run --project src/ScrapGo.Core.Api
   ```

   The API runs on http://localhost:5141, with Swagger at http://localhost:5141/swagger.

2. **Frontend.** The frontend is now implemented and can be run with:

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

   The frontend runs on http://localhost:5174 (or the next available port if 5174 is busy) and connects to the backend API at http://localhost:5141. The API's CORS settings already allow requests from the frontend's development server.

   Key features of the frontend:
   - Login page at `/login` with email/password authentication
   - Dashboard at `/dashboard` with stats cards and activity table
   - Protected routes that require authentication
   - Mobile-responsive design using Tailwind CSS
   - Loading skeletons for better UX during data fetching

### Admin Access

The portal includes administrative features for managing organizations, roles, permissions, and users. There are two levels of administrative access:

1. **Platform Administrator**: A single user with full system access. This role is bootstrapped using the command:

   ```bash
   dotnet run --project src/ScrapGo.Core.Api -- bootstrap-platform-admin --uid <GCIP_UID>
   ```

2. **Organization Administrators**: Users with administrative access limited to specific organizations. These roles can be assigned through the API.

### Recent Work Completed

A summary of recent work to set up administrative access is available in [WORK-DONE.md](WORK-DONE.md).

For detailed information about administrative APIs and features, see:

- Backend admin implementation: [`backend/ADMIN-API-GAPS-v2.md`](backend/ADMIN-API-GAPS-v2.md)
- Frontend admin status: [`frontend/ADMIN-FRONTEND-STATUS.md`](frontend/ADMIN-FRONTEND-STATUS.md)
- Frontend admin gaps: [`frontend/ADMIN-FRONTEND-GAPS.md`](frontend/ADMIN-FRONTEND-GAPS.md)

## Folder Structure

- `/backend`: .NET Web API managing identity, governance, and the Quickbase Proxy/Cache engine. See [`backend/README.md`](backend/README.md).
  - `src/`: the API host and its modules (`Identity`, `QuickbaseEngine`)
  - `tests/`: integration tests (xUnit + Testcontainers)
  - `infra/terraform/`: GCP infrastructure as code
- `/frontend`: React/TanStack Start application (PWA) for stakeholder engagement.

## Guidelines

AI-assistant and contributor rules live in `AGENTS.md` files: [`/AGENTS.md`](AGENTS.md) (whole project), [`/backend/AGENTS.md`](backend/AGENTS.md) and [`/frontend/AGENTS.md`](frontend/AGENTS.md).
