import { useMemo, useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { KeyRound, Pencil, Plus, ShieldCheck, Trash2 } from "lucide-react"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Textarea } from "../ui/textarea"
import { Checkbox } from "../ui/checkbox"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "../ui/table"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../ui/dialog"
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "../ui/alert-dialog"
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "../ui/select"
import { useToast } from "../ui/toast"
import {
    useAttachPermission,
    useCreateRole,
    useDeleteRole,
    useDetachPermission,
    useOrganizations,
    useOrganizationRoles,
    usePermissions,
    useUpdateRole,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { useAdminAccess } from "../../hooks/useAdminAccess"

const schema = z.object({
    organizationId: z.string().min(1, "Select an organization."),
    name: z.string().trim().min(1, "Name is required."),
    description: z.string().trim().optional(),
})

type FormValues = z.infer<typeof schema>

export function RolesPanel() {
    const organizationsQuery = useOrganizations()
    const permissionsQuery = usePermissions()
    const { toast } = useToast()
    const { user, holdsRole, can } = useAdminAccess()

    // For now, we'll use the first organization as default
    // In a real implementation, we'd want to let the user select an organization
    const firstOrganizationId = organizationsQuery.data?.[0]?.id

    const rolesQuery = useOrganizationRoles(firstOrganizationId || 0)

    const createRole = useCreateRole()
    const updateRole = useUpdateRole()
    const deleteRole = useDeleteRole()
    const attachPermission = useAttachPermission()
    const detachPermission = useDetachPermission()

    const [formOpen, setFormOpen] = useState(false)
    const [editing, setEditing] = useState<any | null>(null)
    const [deleting, setDeleting] = useState<any | null>(null)
    const [managing, setManaging] = useState<any | null>(null)

    const organizations = organizationsQuery.data ?? []
    const roles = rolesQuery.data ?? []

    const {
        register,
        handleSubmit,
        reset,
        setValue,
        watch,
        formState: { errors },
    } = useForm<FormValues>({
        resolver: zodResolver(schema),
        defaultValues: { organizationId: "", name: "", description: "" },
    })

    const selectedOrg = watch("organizationId")

    function openCreate() {
        setEditing(null)
        reset({
            organizationId: organizations[0] ? String(organizations[0].id) : "",
            name: "",
            description: "",
        })
        setFormOpen(true)
    }

    function openEdit(role: any) {
        setEditing(role)
        reset({
            organizationId: String(role.organizationId || ""),
            name: role.name,
            description: role.description,
        })
        setFormOpen(true)
    }

    const onSubmit = handleSubmit(async (values) => {
        try {
            if (editing) {
                await updateRole.mutateAsync({
                    id: editing.id,
                    name: values.name,
                    description: values.description,
                    organizationId: Number(values.organizationId),
                })
                toast({ title: "Role updated", variant: "success" })
            } else {
                await createRole.mutateAsync({
                    organizationId: Number(values.organizationId),
                    name: values.name,
                    description: values.description,
                })
                toast({
                    title: "Role created",
                    description: `“${values.name}” was created.`,
                    variant: "success",
                })
            }
            setFormOpen(false)
        } catch (err) {
            toast({
                title: editing ? "Could not update role" : "Could not create role",
                description: adminErrorMessage(err),
                variant: "destructive",
            })
        }
    })

    async function confirmDelete() {
        if (!deleting) return
        try {
            await deleteRole.mutateAsync({
                id: deleting.id,
                organizationId: deleting.organizationId || firstOrganizationId || 0
            })
            toast({ title: "Role deleted", variant: "success" })
            setDeleting(null)
        } catch (err) {
            toast({
                title: "Could not delete role",
                description: adminErrorMessage(err),
                variant: "destructive",
            })
        }
    }

    const isSaving = createRole.isPending || updateRole.isPending

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-lg font-semibold text-ink-900">Roles</h2>
                    <p className="text-sm text-muted-foreground">
                        Custom roles for your organizations, composed from the permission
                        catalog.
                    </p>
                </div>
                <Button onClick={openCreate} disabled={organizations.length === 0}>
                    <Plus className="h-4 w-4" />
                    New role
                </Button>
            </div>

            {/* Warning message removed since we now have server-side role listing */}

            {rolesQuery.isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : roles.length === 0 ? (
                <EmptyState
                    icon={ShieldCheck}
                    title="No roles yet"
                    description={
                        organizations.length === 0
                            ? "Create an organization first, then add roles to it."
                            : "Create a role and compose it from the permission catalog."
                    }
                    action={
                        organizations.length > 0 ? (
                            <Button onClick={openCreate}>
                                <Plus className="h-4 w-4" />
                                New role
                            </Button>
                        ) : undefined
                    }
                />
            ) : (
                <div className="rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Name</TableHead>
                                <TableHead>Description</TableHead>
                                <TableHead>Permissions</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {roles.map((role) => (
                                <TableRow key={role.id}>
                                    <TableCell className="font-medium text-ink-900">
                                        {role.name}
                                    </TableCell>
                                    <TableCell className="max-w-xs truncate text-muted-foreground">
                                        {role.description || "—"}
                                    </TableCell>
                                    <TableCell>
                                        <Badge variant="neutral">
                                            {(role as any).permissions?.length || 0}
                                        </Badge>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex justify-end gap-1">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => setManaging(role)}
                                                disabled={holdsRole(role.id, role.organizationId)}
                                                title={holdsRole(role.id, role.organizationId) ? "You cannot modify a role you hold yourself" : ""}
                                            >
                                                <KeyRound className="h-4 w-4" />
                                                Permissions
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                aria-label="Edit role"
                                                onClick={() => openEdit(role)}
                                                disabled={holdsRole(role.id, role.organizationId)}
                                            >
                                                <Pencil className="h-4 w-4" />
                                            </Button>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                aria-label="Delete role"
                                                onClick={() => setDeleting(role)}
                                                disabled={holdsRole(role.id, role.organizationId)}
                                            >
                                                <Trash2 className="h-4 w-4 text-red-600" />
                                            </Button>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            {/* Create / edit dialog */}
            <Dialog open={formOpen} onOpenChange={setFormOpen}>
                <DialogContent>
                    <form onSubmit={onSubmit}>
                        <DialogHeader>
                            <DialogTitle>{editing ? "Edit role" : "New role"}</DialogTitle>
                            <DialogDescription>
                                {editing
                                    ? "Rename the role or update its description."
                                    : "Roles are scoped to an organization you administer."}
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-4 py-4">
                            <div className="grid gap-2">
                                <Label htmlFor="role-org">Organization</Label>
                                <Select
                                    value={selectedOrg}
                                    onValueChange={(value) =>
                                        setValue("organizationId", value, {
                                            shouldValidate: true,
                                        })
                                    }
                                    disabled={!!editing}
                                >
                                    <SelectTrigger id="role-org">
                                        <SelectValue placeholder="Select an organization" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {organizations.map((org) => (
                                            <SelectItem key={org.id} value={String(org.id)}>
                                                {org.name}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {errors.organizationId && (
                                    <p className="text-sm text-red-600">
                                        {errors.organizationId.message}
                                    </p>
                                )}
                            </div>
                            <div className="grid gap-2">
                                <Label htmlFor="role-name">Name</Label>
                                <Input
                                    id="role-name"
                                    placeholder="Freight Coordinator"
                                    autoComplete="off"
                                    {...register("name")}
                                />
                                {errors.name && (
                                    <p className="text-sm text-red-600">
                                        {errors.name.message}
                                    </p>
                                )}
                            </div>
                            <div className="grid gap-2">
                                <Label htmlFor="role-description">Description</Label>
                                <Textarea
                                    id="role-description"
                                    placeholder="What this role is for…"
                                    {...register("description")}
                                />
                            </div>
                        </div>
                        <DialogFooter>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => setFormOpen(false)}
                            >
                                Cancel
                            </Button>
                            <Button type="submit" disabled={isSaving}>
                                {isSaving ? "Saving…" : editing ? "Save changes" : "Create"}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>

            {/* Delete confirmation */}
            <AlertDialog
                open={!!deleting}
                onOpenChange={(open) => !open && setDeleting(null)}
            >
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>Delete “{deleting?.name}”?</AlertDialogTitle>
                        <AlertDialogDescription>
                            This soft-deletes the role. It cannot be deleted while users are
                            still assigned to it.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel>Cancel</AlertDialogCancel>
                        <AlertDialogAction
                            onClick={(e) => {
                                e.preventDefault()
                                void confirmDelete()
                            }}
                            disabled={deleteRole.isPending}
                        >
                            {deleteRole.isPending ? "Deleting…" : "Delete"}
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            {/* Permission composition */}
            <PermissionDialog
                role={managing}
                permissions={(permissionsQuery.data ?? []).map((p) => p.name)}
                userPermissions={user?.permissions || []}
                organizationId={firstOrganizationId || 0}
                onClose={() => setManaging(null)}
                onAttach={async (roleId, name) => {
                    try {
                        await attachPermission.mutateAsync({
                            roleId,
                            permissionName: name,
                            organizationId: firstOrganizationId || 0
                        })
                        setManaging((current) =>
                            current && current.id === roleId
                                ? {
                                    ...current,
                                    permissions: current.permissions.includes(name)
                                        ? current.permissions
                                        : [...current.permissions, name],
                                }
                                : current
                        )
                    } catch (err) {
                        toast({
                            title: "Could not attach permission",
                            description: adminErrorMessage(err),
                            variant: "destructive",
                        })
                    }
                }}
                onDetach={async (roleId, name) => {
                    try {
                        await detachPermission.mutateAsync({
                            roleId,
                            permissionName: name,
                            organizationId: firstOrganizationId || 0
                        })
                        setManaging((current) =>
                            current && current.id === roleId
                                ? {
                                    ...current,
                                    permissions: current.permissions.filter(
                                        (p) => p !== name
                                    ),
                                }
                                : current
                        )
                    } catch (err) {
                        toast({
                            title: "Could not detach permission",
                            description: adminErrorMessage(err),
                            variant: "destructive",
                        })
                    }
                }}
            />
        </div>
    )
}

function PermissionDialog({
    role,
    permissions,
    userPermissions,
    organizationId,
    onClose,
    onAttach,
    onDetach,
}: {
    role: any | null
    permissions: string[]
    userPermissions: { organizationId: number | null; permissions: string[] }[]
    organizationId: number
    onClose: () => void
    onAttach: (roleId: number, name: string) => Promise<void>
    onDetach: (roleId: number, name: string) => Promise<void>
}) {
    const [pending, setPending] = useState<string | null>(null)

    // Filter permissions to only those the user holds in this organization
    const userOrgPermissions = userPermissions.find(p => p.organizationId === organizationId);
    const availablePermissions = userOrgPermissions
        ? permissions.filter(p => userOrgPermissions.permissions.includes(p))
        : [];

    const grouped = useMemo(() => {
        const groups = new Map<string, string[]>()
        for (const name of availablePermissions) {
            const [resource] = name.split(".")
            const list = groups.get(resource) ?? []
            list.push(name)
            groups.set(resource, list)
        }
        return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
    }, [availablePermissions])

    if (!role) return null

    async function toggle(name: string, checked: boolean) {
        if (!role) return
        setPending(name)
        try {
            if (checked) {
                await onAttach(role.id, name)
            } else {
                await onDetach(role.id, name)
            }
        } finally {
            setPending(null)
        }
    }

    return (
        <Dialog open={!!role} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-h-[85vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Permissions for “{role.name}”</DialogTitle>
                    <DialogDescription>
                        Changes apply immediately. The API now supports read-back of role permissions.
                        Only permissions you hold in this organization are available for assignment.
                    </DialogDescription>
                </DialogHeader>
                <div className="space-y-5 py-2">
                    {grouped.map(([resource, names]) => (
                        <div key={resource} className="space-y-2">
                            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                                {resource}
                            </p>
                            <div className="grid gap-2 sm:grid-cols-2">
                                {names.map((name) => {
                                    // Only show permissions the user holds
                                    if (!availablePermissions.includes(name)) return null;

                                    const checked = role.permissions.includes(name)
                                    return (
                                        <label
                                            key={name}
                                            className="flex cursor-pointer items-center gap-2.5 rounded-lg border border-border px-3 py-2 text-sm transition-colors hover:bg-muted"
                                        >
                                            <Checkbox
                                                checked={checked}
                                                disabled={pending === name}
                                                onCheckedChange={(value) =>
                                                    void toggle(name, value === true)
                                                }
                                            />
                                            <span className="font-mono text-xs">{name}</span>
                                        </label>
                                    )
                                })}
                            </div>
                        </div>
                    ))}
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={onClose}>
                        Done
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}
