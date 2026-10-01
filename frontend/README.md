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

2. Open your browser to http://localhost:5174 (or the next available port if 5174 is busy)

The development server features:

- Hot Module Replacement (HMR) for instant feedback
- Automatic port selection if 5174 is occupied
- API proxy to backend at http://localhost:5141

### Building for Production

```bash
npm run build
```

This creates an optimized production build in the `dist/` directory.

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

- Login page with email/password authentication
- Protected routes that require authentication
- Token-based session management

Authentication is real, not mocked:

1. The login form calls **Google Cloud Identity Platform (GCIP)** via the
   `accounts:signInWithPassword` REST endpoint to obtain an ID token.
2. The token is validated against the backend by calling **`GET /api/users/me`**
   (which also auto-provisions the user on first sight). Only then is the token
   persisted to `localStorage`.
3. On every app load, a stored token is re-validated against `/api/users/me`;
   tokens the backend rejects (expired, disabled user) are discarded.

### Environment Variables

Copy `.env.example` to `.env.local` (gitignored) and set:

```
# Leave empty in development to use the Vite dev proxy (see vite.config.ts),
# which forwards /api to http://localhost:5141 and avoids CORS.
VITE_API_BASE_URL=

# Public GCIP web API key (not a secret).
VITE_GCIP_API_KEY=your-gcip-web-api-key
VITE_GCIP_PROJECT_ID=wrigley-cloud-prod
```

In development the Vite dev server proxies `/api` to the backend at
`http://localhost:5141`, so the browser stays on a single origin and the
backend's CORS allow-list is never exercised.

### Dashboard

- Stats cards showing key metrics
- Recent activity table with loading skeletons
- Responsive layout for mobile and desktop

### Routing

- File-based routing with TanStack Router
- Lazy-loaded routes for performance
- Protected route wrappers

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint
- `npm run routes` - Generate route tree

## Browser Support

The application is designed for modern browsers and optimized for mobile devices. It follows a mobile-first approach with responsive design principles.
