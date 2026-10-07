# Admin Frontend Status

Where the admin portal (`/admin`, `/admin/organizations/$organizationId`,
`/admin/users/$userId`, `/settings`) stands against the backend admin API.

- **Verified:** 2026-10-05 against the code, after commit `31612ca` and the build fixes below.
- **Endpoint contracts:** see [`ADMIN-FRONTEND-GAPS.md`](ADMIN-FRONTEND-GAPS.md).
- **Paths:** relative to `frontend/src/app/`.

## Build health

| Check | Result |
| --- | --- |
| `npm run build` (`tsr generate && tsc -b && vite build`) | **Passes** |
| `npx tsc -p tsconfig.app.json --noEmit` | **0 errors** (was 38 after `31612ca`) |
| `npm run lint` | 15 errors, 2 warnings (was 66 problems). Mostly `react-refresh/only-export-components` in route files (`dashboard`, `index`, `login`, `settings`), which predate the admin work |

**Fixed on 2026-10-05:**
- **New route files moved out of the `/admin` layout.** They are now `routes/admin_.users.$userId.tsx` and `routes/admin_.organizations.$organizationId.tsx`. The old names (`admin.users…`) nested them under `/admin`, which has no `<Outlet/>`, so the pages could never render. The URLs are unchanged.
- **Wrong import paths** (`../../components/…` → `../components/…`).
- **Stale `routeTree.gen.ts`, now regenerated.** The project has no Vite router plugin, so `dev` and `build` now run `tsr generate` first.
- **`roles-panel.tsx`:**
  - The Permissions dialog was opened with a list row that has no `permissions`, so the first attach threw. It now reads the role from `GET …/roles/{id}` (`useOrganizationRoleDetail`), which attach and detach refresh.
  - The "permissions" column always showed 0; it now shows Built-in / Custom.
  - Built-in roles are read-only in the UI (the API answers 404 to writes on them).
- **`Role.organizationId`** added to the `Role` type.
- **No more requests for organization 0 or role 0:** queries are `enabled` only for real ids.
- **Unused imports and variables** removed across the admin panels.

## Feature status

| Feature | Endpoints | Status | Notes |
| --- | --- | --- | --- |
| Admin nav + gate | `GET /api/users/me` (`roles`, `permissions`) | **Done** | `hooks/useAdminAccess.ts`: `can(permission, organizationId \| null)`, `isPlatformAdmin`, `holdsRole`. The nav shows Administration only when `isAdmin`. |
| Browser-local role registry | n/a | **Removed** | `lib/role-registry.ts` deleted; roles come from the API. |
| Organizations list | `GET /api/organizations` | **Done** | The caller's own organizations only. |
| Organization create | `POST /api/admin/organizations` | **Done (platform admins)** | Self-service creation was removed (ORG-APP-MODULE-MODEL.md, Decision 11). The form asks for the first administrator's user id and shows only to platform admins. Invite-by-email is in v2. |
| Organization detail + rename | `GET/PUT /api/organizations/{organizationId}` | **Done** | `components/admin/organization-detail-panel.tsx`. |
| Organization members | `GET/POST/DELETE /api/organizations/{organizationId}/members[/{userId}]` | **Done** | `organization-members-panel.tsx`. Members are added by **numeric user id**; there's no user search for org admins (backend gap). |
| Roles table | `GET /api/organizations/{organizationId}/roles` | **Partial** | Always shows the caller's **first** organization; there's no organization picker yet. |
| Role create / edit / delete | `POST /api/roles`, `PUT/DELETE /api/roles/{id}` | **Done** | Disabled for built-in roles and roles the caller holds. |
| Role permission composition | `POST /api/roles/{id}/permissions`, `DELETE /api/roles/{id}/permissions/{name}`, read-back `GET /api/organizations/{organizationId}/roles/{id}` | **Done** | The picker offers only permissions the caller holds in that organization. |
| Permission catalog | `GET /api/permissions` | **Done** | |
| User administration (platform) | `GET /api/users`, `GET /api/users/{id}`, `POST /api/users/{id}/disable` / `enable` | **Done** | `users-panel.tsx`. The Users tab is visible to every admin, but non-platform admins get 403 from the API; gate it on `isPlatformAdmin`. |
| Role assignment | `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}?organizationId=` | **Partial** | `role-assignment-panel.tsx` at `/admin/users/$userId`. The organization is a free-text id field; it should be a picker. Platform-scope assignment (empty organization) only works for platform admins. |
| Error messages | all `reason` codes | **Done** | `lib/admin-errors.ts` maps every reason the API sends; `not_organization_administrator` is removed. |
| Settings / profile | `GET /api/users/me`, `POST /api/users/me/linked-providers/link` | **Done** | |

## Remaining frontend work

1. **Organization picker** in the roles panel and role assignment, instead of "first organization" and a free-text id.
2. **Gate the Users tab** (and the `/admin/users/$userId` route) on `isPlatformAdmin`.
3. **Refetch `/me` after role or membership changes** that affect the caller, so `can()` stays current. Mutations invalidate admin queries today, not the current-user query.
4. **Lint:** move the non-component exports out of the route files (`react-refresh/only-export-components`).
5. **Code-split** the 700 kB bundle (Vite warns over 500 kB).
6. **Application and module administration, invitations, and the `/me` application tree:** see [`ADMIN-FRONTEND-GAPS-v2.md`](ADMIN-FRONTEND-GAPS-v2.md).

## Endpoint reference (correct paths)

- Detach a permission from a role: **`DELETE /api/roles/{id}/permissions/{name}`** (path segment, URL-encoded), not a query string.
- Revoke a role: `DELETE /api/users/{id}/roles/{roleId}?organizationId={organizationId}`. Omit `organizationId` for platform scope.
- Role reads are organization-scoped: `GET /api/organizations/{organizationId}/roles[/{id}[/permissions]]`.
