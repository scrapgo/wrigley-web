// Browser-local registry of roles the current user has created.
//
// WHY THIS EXISTS: the API exposes role create/update/delete and permission
// attach/detach, but NOT `GET /api/roles` or `GET /api/roles/{id}/permissions`.
// Without a read endpoint the UI cannot list roles or show what a role grants,
// so we remember locally what this browser created. Roles created elsewhere are
// invisible here.
//
// REMOVE THIS once `GET /api/roles` and `GET /api/roles/{id}/permissions` land
// (see backend/ADMIN-API-GAPS.md).

const STORAGE_KEY = 'scrapgo.admin.roles'

export interface RegisteredRole {
    id: number
    name: string
    description: string
    organizationId: number
    /** Permission names attached via this browser. Not authoritative. */
    permissions: string[]
    createdAt: string
}

function read(): RegisteredRole[] {
    try {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (!raw) return []
        const parsed = JSON.parse(raw) as RegisteredRole[]
        return Array.isArray(parsed) ? parsed : []
    } catch {
        return []
    }
}

function write(roles: RegisteredRole[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(roles))
}

export const roleRegistry = {
    list(): RegisteredRole[] {
        return read().sort((a, b) => a.name.localeCompare(b.name))
    },

    get(id: number): RegisteredRole | undefined {
        return read().find((role) => role.id === id)
    },

    add(role: Omit<RegisteredRole, 'permissions' | 'createdAt'>): RegisteredRole {
        const entry: RegisteredRole = {
            ...role,
            permissions: [],
            createdAt: new Date().toISOString(),
        }
        write([...read().filter((r) => r.id !== role.id), entry])
        return entry
    },

    update(id: number, changes: Partial<Pick<RegisteredRole, 'name' | 'description'>>): void {
        write(read().map((role) => (role.id === id ? { ...role, ...changes } : role)))
    },

    remove(id: number): void {
        write(read().filter((role) => role.id !== id))
    },

    setPermissions(id: number, permissions: string[]): void {
        write(read().map((role) => (role.id === id ? { ...role, permissions } : role)))
    },

    addPermission(id: number, permission: string): void {
        write(
            read().map((role) =>
                role.id === id && !role.permissions.includes(permission)
                    ? { ...role, permissions: [...role.permissions, permission] }
                    : role
            )
        )
    },

    removePermission(id: number, permission: string): void {
        write(
            read().map((role) =>
                role.id === id
                    ? { ...role, permissions: role.permissions.filter((p) => p !== permission) }
                    : role
            )
        )
    },
}
