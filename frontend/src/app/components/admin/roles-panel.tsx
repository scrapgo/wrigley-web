import { useState } from "react"
import { ArrowLeft, Loader2, ShieldCheck } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Checkbox } from "../ui/checkbox"
import { EmptyState } from "../ui/empty-state"
import { Link } from "@tanstack/react-router"
import { Skeleton } from "../ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { useToast } from "../ui/toast"
import { useAccess } from "../../hooks/useAccess"
import { usePermissionGroups, useRoles, useSetRolePermissions } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { HelpButton } from "../ui/help-button"
import type { Permission, Role } from "../../lib/admin-api"

/** The audience rule: Internal permissions on internal roles, External on external, Any on both. */
function fits(permission: Permission, role: Role) {
    return permission.audience === "Any" || permission.audience === role.userType
}

/**
 * Roles tab (roles.view): a list of roles, each with a count of the
 * permissions it grants. Click a role to see and edit exactly which
 * permissions it grants — one role at a time, so nothing is overwhelming.
 * Read-only without roles.manage.
 */
export function RolesPanel() {
    const { can } = useAccess()
    const canManage = can("roles.manage")
    const { toast } = useToast()
    const roles = useRoles()
    const groups = usePermissionGroups()
    const save = useSetRolePermissions()
    // The role currently being edited, and its draft permissions.
    const [editingRoleId, setEditingRoleId] = useState<number | null>(null)
    const [drafts, setDrafts] = useState<Record<number, Set<string>>>({})
    const [savingRoleId, setSavingRoleId] = useState<number | null>(null)

    if (roles.isLoading || groups.isLoading) {
        return <Skeleton className="h-96 w-full" />
    }

    if (roles.isError || groups.isError || !roles.data || !groups.data) {
        return <EmptyState icon={ShieldCheck} title="Could not load roles" description={adminErrorMessage(roles.error ?? groups.error)} />
    }

    // A role being edited: show the drill-in view.
    if (editingRoleId !== null) {
        const role = roles.data.find((r) => r.id === editingRoleId)
        if (!role) {
            setEditingRoleId(null)
        } else {
            return (
                <RoleEditor
                    role={role}
                    groups={groups.data}
                    canManage={canManage}
                    save={save}
                    drafts={drafts}
                    setDrafts={setDrafts}
                    savingRoleId={savingRoleId}
                    setSavingRoleId={setSavingRoleId}
                    onBack={() => setEditingRoleId(null)}
                    toast={toast}
                />
            )
        }
    }

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between gap-2">
                <h2 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    <ShieldCheck className="h-3.5 w-3.5" />
                    Roles
                </h2>
                <HelpButton
                    title="Roles"
                    description="A role is a bundle of permissions. Every user holds one or more roles, and a role's permissions decide what they can do in ScrapGo. Click a role to see which permissions it grants and to change them. Saving applies to everyone who holds that role within 30 seconds."
                />
            </div>
            {!canManage && <p className="text-xs text-muted-foreground">Read-only: changing roles needs the roles.manage permission.</p>}
            <div className="rounded-xl border border-border bg-card">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead className="text-xs">Role</TableHead>
                            <TableHead className="text-center text-xs">Type</TableHead>
                            <TableHead className="text-center text-xs">Permissions</TableHead>
                            <TableHead className="text-center text-xs">Users</TableHead>
                            <TableHead className="text-right text-xs">Action</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {roles.data.map((role) => (
                            <TableRow key={role.id} className="cursor-pointer hover:bg-muted/50" onClick={() => setEditingRoleId(role.id)}>
                                <TableCell className="font-medium text-ink-900 text-sm">{role.name}</TableCell>
                                <TableCell className="text-center">
                                    <Badge variant={role.userType === "Internal" ? "success" : "neutral"} className="text-[0.65rem]">
                                        {role.userType}
                                    </Badge>
                                </TableCell>
                                <TableCell className="text-center text-muted-foreground text-sm">{role.permissions.length}</TableCell>
                                <TableCell className="text-center text-muted-foreground text-sm">{role.holderCount}</TableCell>
                                <TableCell className="text-right">
                                    {canManage && (
                                        <Button variant="ghost" size="icon" onClick={(e) => {
                                            e.stopPropagation()
                                            setEditingRoleId(role.id)
                                        }}>
                                            <ShieldCheck className="h-3.5 w-3.5" />
                                        </Button>
                                    )}
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </div>
            {roles.data.length === 0 && <EmptyState icon={ShieldCheck} title="No roles" description="There are no roles to manage." />}
        </div>
    )
}

/** Edit one role's permissions, grouped by feature. */
function RoleEditor({
    role,
    groups,
    canManage,
    save,
    drafts,
    setDrafts,
    savingRoleId,
    setSavingRoleId,
    onBack,
    toast,
}: {
    role: Role
    groups: { featureKey: string; featureName: string; permissions: Permission[] }[]
    canManage: boolean
    save: ReturnType<typeof useSetRolePermissions>
    drafts: Record<number, Set<string>>
    setDrafts: React.Dispatch<React.SetStateAction<Record<number, Set<string>>>>
    savingRoleId: number | null
    setSavingRoleId: React.Dispatch<React.SetStateAction<number | null>>
    onBack: () => void
    toast: ReturnType<typeof useToast>["toast"]
}) {
    const granted = drafts[role.id] ?? new Set(role.permissions)
    const isDirty =
        drafts[role.id] !== undefined &&
        (granted.size !== role.permissions.length || role.permissions.some((key) => !granted.has(key)))

    const toggle = (key: string, checked: boolean) =>
        setDrafts((current) => {
            const next = new Set(current[role.id] ?? role.permissions)
            if (checked) next.add(key)
            else next.delete(key)
            return { ...current, [role.id]: next }
        })

    const onSave = async () => {
        setSavingRoleId(role.id)
        try {
            await save.mutateAsync({ id: role.id, permissions: [...granted] })
            setDrafts((current) => {
                const next = { ...current }
                delete next[role.id]
                return next
            })
            toast({ title: `${role.name} saved`, variant: "success" })
            onBack()
        } catch (err) {
            toast({ title: `Could not save ${role.name}`, description: adminErrorMessage(err), variant: "destructive" })
        } finally {
            setSavingRoleId(null)
        }
    }

    return (
        <div className="space-y-3">
            <Button variant="ghost" asChild onClick={onBack}>
                <Link to="/admin" search={{ tab: "access" }}>
                    <ArrowLeft className="h-4 w-4" />
                    All roles
                </Link>
            </Button>
            <div className="flex items-center justify-between gap-2">
                <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                    <ShieldCheck className="h-4 w-4" />
                    {role.name}
                </h2>
                <Badge variant={role.userType === "Internal" ? "success" : "neutral"} className="text-[0.75rem]">
                    {role.userType}
                </Badge>
            </div>
            <p className="text-sm text-muted-foreground">
                {role.permissions.length} permission{role.permissions.length === 1 ? "" : "s"} · {role.holderCount} user{role.holderCount === 1 ? "" : "s"} hold this role
            </p>
            {!canManage && <p className="text-sm text-muted-foreground">Read-only: changing roles needs the roles.manage permission.</p>}
            <div className="space-y-3">
                {groups.map((group) => {
                    const allowed = group.permissions.filter((p) => fits(p, role))
                    if (allowed.length === 0) return null
                    return (
                        <div key={group.featureKey} className="rounded-xl border border-border bg-card">
                            <div className="border-b border-border bg-muted/50 px-3 py-2">
                                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.featureName}</p>
                            </div>
                            <div className="divide-y divide-border p-3">
                                {allowed.map((permission) => (
                                    <label
                                        key={permission.key}
                                        className="flex items-center justify-between gap-3 rounded-lg border border-transparent p-2 hover:bg-muted/50 cursor-pointer"
                                    >
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-medium text-ink-900 truncate">{permission.name}</p>
                                            <p className="text-xs text-muted-foreground truncate">{permission.key}</p>
                                        </div>
                                        <Checkbox
                                            checked={granted.has(permission.key)}
                                            disabled={!canManage}
                                            onCheckedChange={(checked) => toggle(permission.key, checked === true)}
                                            aria-label={`${role.name}: ${permission.name}`}
                                        />
                                    </label>
                                ))}
                            </div>
                        </div>
                    )
                })}
            </div>
            {canManage && (
                <div className="pt-4">
                    <Button
                        size="lg"
                        disabled={!isDirty || savingRoleId !== null}
                        onClick={() => void onSave()}
                        className="w-full"
                    >
                        {savingRoleId === role.id && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
                        Save {role.name}
                    </Button>
                </div>
            )}
        </div>
    )
}
