# Admin Frontend Status

This document tracks the current status of admin frontend implementation, reflecting what has been completed and what remains to be done.

## Current Status

As of 2026-10-05, the backend admin API has been fully implemented (Tasks 1-10 completed). The frontend now needs to be updated to use the real permissions system instead of the interim admin gate.

## Completed Backend Work

The following backend features are now available:

1. **User Management**
   - `GET /api/users/me` now includes roles and per-scope permissions
   - Platform-scoped user management: `GET /api/users`, `GET /api/users/{id}`
   - User enable/disable: `POST /api/users/{id}/disable`, `POST /api/users/{id}/enable`

2. **Role Management**
   - Organization-scoped role reads: `GET /api/organizations/{organizationId}/roles`, `GET /api/organizations/{organizationId}/roles/{id}`, `GET /api/organizations/{organizationId}/roles/{id}/permissions`
   - Role assignment: `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}?organizationId=`

3. **Organization Management**
   - Organization CRUD: `GET/PUT /api/organizations/{organizationId}`
   - Membership management: `GET/POST/DELETE /api/organizations/{organizationId}/members/{userId}`

4. **Permission System**
   - Platform Administrator role with `Admin.Access` permission
   - Organization Administrator role with comprehensive org-level permissions
   - Bootstrap command for first Platform Administrator

## Frontend Implementation Status

### 1. Critical: Replace Interim Admin Gate (Priority 1)

**Status**: Incomplete
**Files affected**: `hooks/useAdminAccess.ts`, `lib/api-client.ts`

The current implementation shows admin features to every authenticated user. This needs to be replaced with real permission checking:

```ts
interface CurrentUser {
  id: number;
  identityPlatformUid: string;
  email: string;
  status: string;
  classification: string;
  roles: { roleId: number; name: string; organizationId: number | null }[];
  // One entry per scope the caller holds anything in. organizationId null = platform scope.
  permissions: { organizationId: number | null; permissions: string[] }[];
}
```

Implementation requirements:

- Platform admin: `Admin.Access` in the entry where `organizationId === null`
- Organization admin views: Check permissions in that organization's entry
- Add helper function `can(permission, organizationId | null)` for nav items, route guards and buttons
- After any role or membership change, refetch `/me` (invalidate its query)

### 2. Delete Browser-Local Role Registry (Priority 2)

**Status**: Incomplete
**Files affected**: `lib/role-registry.ts`, `hooks/useAdminQueries.ts`

The read endpoints that were missing now exist. Remove the local registry and replace with server queries:

- Remove `lib/role-registry.ts`
- Replace `useRegisteredRoles` with server queries

### 3. Implement Organization-Scoped Admin Features (Priority 3)

**Status**: Incomplete
**Files affected**: Various admin components

With the user having OrganizationAdministrator role for organization ID 1, implement the following:

#### 3.1 Organization Management

- List organizations: `GET /api/organizations`
- Organization detail: `GET /api/organizations/{organizationId}`
- Update organization: `PUT /api/organizations/{organizationId}` (requires `Organization.Update`)

#### 3.2 Role Management within Organization

- List roles: `GET /api/organizations/{organizationId}/roles`
- Role detail: `GET /api/organizations/{organizationId}/roles/{id}`
- Role permissions: `GET /api/organizations/{organizationId}/roles/{id}/permissions`
- Create role: `POST /api/roles` (with organizationId in body)
- Update role: `PUT /api/roles/{id}`
- Delete role: `DELETE /api/roles/{id}`
- Attach permission: `POST /api/roles/{id}/permissions`
- Detach permission: `DELETE /api/roles/{id}/permissions?permissionName=`

#### 3.3 User Management within Organization

- List members: `GET /api/organizations/{organizationId}/members`
- Add member: `POST /api/organizations/{organizationId}/members/{userId}`
- Remove member: `DELETE /api/organizations/{organizationId}/members/{userId}`
- Assign role: `POST /api/users/{id}/roles`
- Revoke role: `DELETE /api/users/{id}/roles/{roleId}?organizationId=`

### 4. Implement Platform Admin Features (Priority 4)

**Status**: Incomplete
**Note**: Requires Platform Administrator access

#### 4.1 Platform User Management

- List users: `GET /api/users?search=&status=&page=&pageSize=`
- User detail: `GET /api/users/{id}`
- Disable/enable users: `POST /api/users/{id}/disable`, `POST /api/users/{id}/enable`

#### 4.2 Platform Role Management

- Manage platform-scoped roles and permissions
- Assign platform-level roles to users

## Implementation Guidelines

### Permission Checking

Never flatten scopes. For example, `Role.Update` in org 12 grants nothing in org 15 or at platform scope. An organization appears only while the caller's membership there is active.

### Error Handling

Update error handling to account for new error reasons:

- `cannot_modify_own_role`
- `cannot_grant_unheld_permission`
- `last_organization_administrator`
- `last_platform_administrator`
- `cannot_disable_self`

### UI/UX Considerations

- Disable actions for roles the user holds themselves
- Only offer permissions in the permission picker that the user actually holds in that organization
- Show built-in roles (organizationId === null) as read-only
- Implement proper loading states and error handling

## Next Steps

1. **Immediate**: Replace interim admin gate with real permission checking
2. **Short-term**: Remove browser-local role registry and implement server queries
3. **Medium-term**: Implement organization-scoped admin features using available permissions
4. **Long-term**: Implement platform admin features (requires Platform Administrator access)

## Testing

All backend endpoints are covered by integration tests. Frontend implementation should be tested against these endpoints to ensure proper functionality and error handling.
