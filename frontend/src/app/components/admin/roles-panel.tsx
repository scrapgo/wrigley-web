import { Fragment, useState } from "react"
import { Loader2, ShieldCheck } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Checkbox } from "../ui/checkbox"
import { EmptyState } from "../ui/empty-state"
import { Skeleton } from "../ui/skeleton"
import { useToast } from "../ui/toast"
import { useAccess } from "../../hooks/useAccess"
import { usePermissionGroups, useRoles, useSetRolePermissions } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import type { Permission, Role } from "../../lib/admin-api"

/** The audience rule: Internal permissions on internal roles, External on external, Any on both. */
function fits(permission: Permission, role: Role) {
    return permission.audience === "Any" || permission.audience === role.userType
}

/**
 * Roles tab (roles.view): permissions grouped by feature down the side, roles
 * across the top, a checkbox at each crossing. Saved per role; read-only
 * without roles.manage.
 */
export function RolesPanel() {
    const { can } = useAccess()
    const canManage = can("roles.manage")
    const { toast } = useToast()
    const roles = useRoles()
    const groups = usePermissionGroups()
    const save = useSetRolePermissions()
    // Unsaved edits per role id; a role without an entry shows what it grants.
    const [drafts, setDrafts] = useState<Record<number, Set<string>>>({})
    const [savingRoleId, setSavingRoleId] = useState<number | null>(null)

    if (roles.isLoading || groups.isLoading) {
        return <Skeleton className="h-96 w-full" />
    }

    if (roles.isError || groups.isError || !roles.data || !groups.data) {
        return <EmptyState icon={ShieldCheck} title="Could not load roles" description={adminErrorMessage(roles.error ?? groups.error)} />
    }

    const granted = (role: Role) => drafts[role.id] ?? new Set(role.permissions)
    const isDirty = (role: Role) => {
        const draft = drafts[role.id]
        return draft !== undefined && (draft.size !== role.permissions.length || role.permissions.some((key) => !draft.has(key)))
    }

    const toggle = (role: Role, key: string, checked: boolean) =>
        setDrafts((current) => {
            const next = new Set(current[role.id] ?? role.permissions)
            if (checked) next.add(key)
            else next.delete(key)
            return { ...current, [role.id]: next }
        })

    const onSave = async (role: Role) => {
        setSavingRoleId(role.id)
        try {
            await save.mutateAsync({ id: role.id, permissions: [...granted(role)] })
            setDrafts((current) => {
                const next = { ...current }
                delete next[role.id]
                return next
            })
            toast({ title: `${role.name} saved`, variant: "success" })
        } catch (err) {
            toast({ title: `Could not save ${role.name}`, description: adminErrorMessage(err), variant: "destructive" })
        } finally {
            setSavingRoleId(null)
        }
    }

    return (
        <div className="space-y-3">
            {!canManage && <p className="text-sm text-muted-foreground">Read-only: changing roles needs the roles.manage permission.</p>}
            <div className="overflow-x-auto rounded-xl border border-border bg-card">
                <table className="w-full min-w-[40rem] text-sm">
                    <thead>
                        <tr className="border-b border-border">
                            <th className="px-4 py-3 text-left font-semibold text-ink-900">Permission</th>
                            {roles.data.map((role) => (
                                <th key={role.id} className="px-4 py-3 text-center align-top">
                                    <span className="block font-semibold text-ink-900">{role.name}</span>
                                    <Badge variant={role.userType === "Internal" ? "success" : "neutral"} className="mt-1">
                                        {role.userType}
                                    </Badge>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {groups.data.map((group) => (
                            <Fragment key={group.featureKey}>
                                <tr className="bg-muted/50">
                                    <td colSpan={roles.data.length + 1} className="px-4 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                                        {group.featureName}
                                    </td>
                                </tr>
                                {group.permissions.map((permission) => (
                                    <tr key={permission.key} className="border-b border-border last:border-0">
                                        <td className="px-4 py-2">
                                            <span className="block font-medium text-ink-900">{permission.name}</span>
                                            <span className="block text-xs text-muted-foreground">
                                                {permission.key} · {permission.audience}
                                            </span>
                                        </td>
                                        {roles.data.map((role) => {
                                            const allowed = fits(permission, role)
                                            return (
                                                <td key={role.id} className={allowed ? "px-4 py-2 text-center" : "bg-muted/60 px-4 py-2 text-center"}>
                                                    {allowed ? (
                                                        <span className="inline-flex min-h-11 min-w-11 items-center justify-center">
                                                            <Checkbox
                                                                checked={granted(role).has(permission.key)}
                                                                disabled={!canManage}
                                                                onCheckedChange={(checked) => toggle(role, permission.key, checked === true)}
                                                                aria-label={`${role.name}: ${permission.name}`}
                                                            />
                                                        </span>
                                                    ) : (
                                                        <span
                                                            className="text-xs text-muted-foreground"
                                                            title={`${permission.audience} permissions can't go on ${role.userType.toLowerCase()} roles`}
                                                        >
                                                            —
                                                        </span>
                                                    )}
                                                </td>
                                            )
                                        })}
                                    </tr>
                                ))}
                            </Fragment>
                        ))}
                    </tbody>
                    {canManage && (
                        <tfoot>
                            <tr className="border-t border-border">
                                <td className="px-4 py-3 text-xs text-muted-foreground">Saving applies to every holder within 30 seconds.</td>
                                {roles.data.map((role) => (
                                    <td key={role.id} className="px-4 py-3 text-center">
                                        <Button size="sm" disabled={!isDirty(role) || savingRoleId !== null} onClick={() => void onSave(role)}>
                                            {savingRoleId === role.id && <Loader2 className="h-4 w-4 animate-spin" />}
                                            Save
                                        </Button>
                                    </td>
                                ))}
                            </tr>
                        </tfoot>
                    )}
                </table>
            </div>
        </div>
    )
}
