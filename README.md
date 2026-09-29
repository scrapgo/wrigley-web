# ScrapGo Downstream Portal

## Overview

Downstream is the centralized operational portal for ScrapGo. It acts as an intelligent, secure bridge between disparate systems (primarily **Quickbase**) and our stakeholders (Suppliers, Carriers, and Internal Staff).

This platform consolidates identity management, pricing transparency, and operational workflow into a mobile-first, PWA-based portal.

## Architecture

- **Backend:** .NET 10 / PostgreSQL (Modular Monolith)

- **Frontend:** React / TanStack Start / Tailwind / Shadcn UI

- **Integration:** Quickbase (System of Record)

## Getting Started

### Prerequisites

- .NET 10 SDK

- Node.js (v20+)

- PostgreSQL 15+

### Running Locally

1. **Backend:**

&#x20; `cd backend && dotnet run`

2. **Frontend:**

&#x20; `cd frontend && npm run dev`

## Folder Structure

- `/backend`: .NET Web API managing identity, governance, and the Quickbase Proxy/Cache engine.

- `/frontend`: React/TanStack Start application (PWA) for stakeholder engagement.
