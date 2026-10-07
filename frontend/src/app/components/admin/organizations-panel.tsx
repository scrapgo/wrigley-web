import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { AppWindow, Building2, Copy, Pencil, Plus, Search, Trash2 } from "lucide-react"
import { Link } from "@tanstack/react-router"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
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
import { useToast } from "../ui/toast"
import {
    useAllOrganizations,
    useCreateOrganization,
    useOrganizations,
    useSetOrganizationActive,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { useAdminAccess } from "../../hooks/useAdminAccess"
import { findUserByEmail, type OrganizationSummary } from "../../lib/admin-api"
import { OrganizationApplicationsDialog } from "./organization-applications-dialog"
import { DeleteOrganizationDialog, EditOrganizationDialog } from "./organization-edit-dialogs"

// The first administrator is an existing user (by id) or an email (invitation).
const schema = z.object({
    name: z
        .string()
        .trim()
        .min(1, "Name is required.")
        .regex(/[a-zA-Z0-9]/, "Name must contain at least one letter or digit."),
    firstAdmin: z
        .string()
        .trim()
        .refine(
            (v) => /^[1-9][0-9]*$/.test(v) || z.string().email().safeParse(v).success,
            "Enter the first administrator's user id or email address."
        ),
})

type FormValues = z.infer<typeof schema>

const STATUS_FILTERS = [
    { value: "", label: "All" },
    { value: "Active", label: "Active" },
    { value: "Disabled", label: "Deactivated" },
]

export function OrganizationsPanel() {
    const { isPlatformAdmin, can, user } = useAdminAccess()
    const [search, setSearch] = useState("")
    const [status, setStatus] = useState("")
    // Platform admins see every organization; everyone else, the ones they belong to.
    const all = useAllOrganizations(search.trim(), status, isPlatformAdmin)
    const mine = useOrganizations()
    const { data, isLoading, isError, error } = isPlatformAdmin
        ? { ...all, data: all.data?.items }
        : mine

    const createOrganization = useCreateOrganization()
    const setActive = useSetOrganizationActive()
    const { toast } = useToast()
    const [open, setOpen] = useState(false)
    const [invitationToken, setInvitationToken] = useState<string | null>(null)
    const [applicationsFor, setApplicationsFor] = useState<OrganizationSummary | null>(null)
    const [editing, setEditing] = useState<OrganizationSummary | null>(null)
    const [deleting, setDeleting] = useState<OrganizationSummary | null>(null)

    const canDeactivate = can("Organization.Deactivate", null)
    const canEdit = can("Organization.Create", null)
    const canAssignApplications = can("Application.Assign", null) || can("Module.Manage", null)
    const memberOf = new Set((user?.organizations ?? []).map((o) => o.organizationId))

    const {
        register,
        handleSubmit,
        reset,
        formState: { errors },
    } = useForm<FormValues>({
        resolver: zodResolver(schema),
        defaultValues: { name: "", firstAdmin: "" },
    })

    const onSubmit = handleSubmit(async (values) => {
        try {
            // An email that belongs to an existing user makes them administrator
            // right away; only an unknown email creates an invitation.
            const existing = /^[1-9][0-9]*$/.test(values.firstAdmin)
                ? Number(values.firstAdmin)
                : (await findUserByEmail(values.firstAdmin))?.id
            const created = await createOrganization.mutateAsync(
                existing !== undefined
                    ? { name: values.name, firstAdminUserId: existing }
                    : { name: values.name, firstAdminEmail: values.firstAdmin }
            )
            toast({
                title: "Organization created",
                description: `“${values.name}” is ready.`,
                variant: "success",
            })
            reset()
            setOpen(false)
            if (created.invitation?.token) {
                setInvitationToken(created.invitation.token)
            }
        } catch (err) {
            toast({
                title: "Could not create organization",
                description: adminErrorMessage(err),
                variant: "destructive",
            })
        }
    })

    const toggleActive = async (org: OrganizationSummary) => {
        const activate = org.status !== "Active"
        try {
            await setActive.mutateAsync({ organizationId: org.id, active: activate })
            toast({
                title: activate ? "Organization reactivated" : "Organization deactivated",
                description: org.name,
                variant: "success",
            })
        } catch (err) {
            toast({ title: "Could not change the organization", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                    <h2 className="text-lg font-semibold text-ink-900">Organizations</h2>
                    <p className="text-sm text-muted-foreground">
                        {isPlatformAdmin ? "Every organization on the platform." : "Organizations you belong to."}
                    </p>
                </div>
                {isPlatformAdmin && (
                    <Button onClick={() => setOpen(true)}>
                        <Plus className="h-4 w-4" />
                        New organization
                    </Button>
                )}
            </div>

            {isPlatformAdmin && (
                <div className="flex flex-col gap-2 sm:flex-row">
                    <div className="relative flex-1">
                        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                        <Input
                            className="pl-9"
                            placeholder="Search organizations"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                    <div className="flex gap-1 rounded-lg border border-border bg-card p-1">
                        {STATUS_FILTERS.map((f) => (
                            <Button
                                key={f.value}
                                size="sm"
                                variant={status === f.value ? "default" : "ghost"}
                                onClick={() => setStatus(f.value)}
                            >
                                {f.label}
                            </Button>
                        ))}
                    </div>
                </div>
            )}

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState
                    icon={Building2}
                    title="Could not load organizations"
                    description={adminErrorMessage(error)}
                />
            ) : !data || data.length === 0 ? (
                <EmptyState
                    icon={Building2}
                    title={search || status ? "No matching organizations" : "No organizations yet"}
                    description={
                        isPlatformAdmin
                            ? "Create an organization and name its first administrator."
                            : "You aren't a member of any organization yet. A platform administrator adds you."
                    }
                    action={
                        isPlatformAdmin && !search && !status ? (
                            <Button onClick={() => setOpen(true)}>
                                <Plus className="h-4 w-4" />
                                New organization
                            </Button>
                        ) : undefined
                    }
                />
            ) : (
                <div className="overflow-x-auto rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Name</TableHead>
                                <TableHead>Slug</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {data.map((org) => (
                                <TableRow key={org.id}>
                                    <TableCell className="font-medium text-ink-900">
                                        {org.name}
                                    </TableCell>
                                    <TableCell className="font-mono text-xs text-muted-foreground">
                                        {org.slug}
                                    </TableCell>
                                    <TableCell>
                                        <Badge variant={org.status === "Active" ? "success" : "neutral"}>
                                            {org.status === "Active" ? "Active" : "Deactivated"}
                                        </Badge>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex flex-wrap justify-end gap-2">
                                            {isPlatformAdmin && canEdit && (
                                                <Button variant="outline" size="sm" onClick={() => setEditing(org)}>
                                                    <Pencil className="h-4 w-4" />
                                                    Edit
                                                </Button>
                                            )}
                                            {isPlatformAdmin && canAssignApplications && (
                                                <Button variant="outline" size="sm" onClick={() => setApplicationsFor(org)}>
                                                    <AppWindow className="h-4 w-4" />
                                                    Applications
                                                </Button>
                                            )}
                                            {isPlatformAdmin && canDeactivate && (
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    disabled={setActive.isPending}
                                                    onClick={() => void toggleActive(org)}
                                                >
                                                    {org.status === "Active" ? "Deactivate" : "Reactivate"}
                                                </Button>
                                            )}
                                            {/* Delete is offered only once deactivated: a deliberate two-step action. */}
                                            {isPlatformAdmin && canDeactivate && org.status !== "Active" && (
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-red-700 hover:bg-red-50"
                                                    onClick={() => setDeleting(org)}
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                    Delete
                                                </Button>
                                            )}
                                            {/* Organization pages need membership, even for platform admins. */}
                                            {(!isPlatformAdmin || memberOf.has(org.id)) && (
                                                <Button variant="outline" size="sm" asChild>
                                                    <Link to="/admin/organizations/$organizationId" params={{ organizationId: org.id.toString() }}>
                                                        Manage
                                                    </Link>
                                                </Button>
                                            )}
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            <OrganizationApplicationsDialog
                organization={applicationsFor}
                onOpenChange={(o) => !o && setApplicationsFor(null)}
            />
            <EditOrganizationDialog organization={editing} onOpenChange={(o) => !o && setEditing(null)} />
            <DeleteOrganizationDialog organization={deleting} onOpenChange={(o) => !o && setDeleting(null)} />

            <Dialog open={open} onOpenChange={setOpen}>
                <DialogContent>
                    <form onSubmit={onSubmit}>
                        <DialogHeader>
                            <DialogTitle>New organization</DialogTitle>
                            <DialogDescription>
                                The name is turned into a unique slug. The first administrator
                                is a user id or email. A user who has signed in before becomes
                                administrator immediately; an unknown email gets an invitation.
                                You are not made a member.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-2 py-4">
                            <Label htmlFor="org-name">Name</Label>
                            <Input
                                id="org-name"
                                placeholder="Midwest Metals"
                                autoComplete="off"
                                {...register("name")}
                            />
                            {errors.name && (
                                <p className="text-sm text-red-600">{errors.name.message}</p>
                            )}
                            <Label htmlFor="org-first-admin">First administrator (user id or email)</Label>
                            <Input
                                id="org-first-admin"
                                placeholder="42 or admin@midwestmetals.com"
                                autoComplete="off"
                                {...register("firstAdmin")}
                            />
                            {errors.firstAdmin && (
                                <p className="text-sm text-red-600">{errors.firstAdmin.message}</p>
                            )}
                        </div>
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                                Cancel
                            </Button>
                            <Button type="submit" disabled={createOrganization.isPending}>
                                {createOrganization.isPending ? "Creating…" : "Create"}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>

            <Dialog open={invitationToken !== null} onOpenChange={(o) => !o && setInvitationToken(null)}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Invitation created</DialogTitle>
                        <DialogDescription>
                            Send this token to the first administrator. After signing in with
                            that email address they open Settings → Accept an invitation (or
                            /invitations?token=…) and paste it. It is shown only once.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="flex items-center gap-2 py-4">
                        <code className="flex-1 break-all rounded-md bg-muted px-3 py-2 text-xs">{invitationToken}</code>
                        <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                                if (invitationToken) void navigator.clipboard.writeText(invitationToken)
                                toast({ title: "Copied", variant: "success" })
                            }}
                        >
                            <Copy className="h-4 w-4" />
                        </Button>
                    </div>
                    <DialogFooter>
                        <Button onClick={() => setInvitationToken(null)}>Done</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    )
}
