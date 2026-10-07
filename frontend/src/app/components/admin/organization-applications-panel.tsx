import { useState } from "react"
import { AppWindow, X } from "lucide-react"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { useToast } from "../ui/toast"
import {
    useApplicationAccess,
    useApplicationRoles,
    useGrantApplicationRole,
    useOrganizationApplications,
    useOrganizationMembers,
    useRevokeApplicationRole,
} from "../../hooks/useAdminQueries"
import { useAdminAccess } from "../../hooks/useAdminAccess"
import { adminErrorMessage } from "../../lib/admin-errors"
import type { OrganizationApplication } from "../../lib/admin-api"

const selectClass =
    "h-11 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"

/**
 * The organization's applications. Application administrators (holding
 * Application.ManageAccess there) grant and revoke access; organization
 * administrators can't (Decision 3) and only see which applications exist.
 */
export function OrganizationApplicationsPanel({ organizationId }: { organizationId: number }) {
    const { data, isLoading, isError, error } = useOrganizationApplications(organizationId)
    const { isAppAdmin } = useAdminAccess()

    return (
        <Card>
            <CardHeader>
                <CardTitle className="flex items-center gap-2">
                    <AppWindow className="h-5 w-5" />
                    Applications
                </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
                {isLoading ? (
                    <Skeleton className="h-20 w-full" />
                ) : isError ? (
                    <EmptyState icon={AppWindow} title="Could not load applications" description={adminErrorMessage(error)} />
                ) : !data || data.length === 0 ? (
                    <EmptyState
                        icon={AppWindow}
                        title="No applications"
                        description="A platform administrator assigns applications to this organization."
                    />
                ) : (
                    data.map((application) => (
                        <div key={application.applicationId} className="rounded-lg border border-border p-4">
                            <div className="flex flex-wrap items-center gap-2">
                                <p className="font-semibold text-ink-900">{application.name}</p>
                                {application.modules.length === 0 ? (
                                    <Badge variant="warning">No modules enabled</Badge>
                                ) : (
                                    application.modules.map((m) => (
                                        <Badge key={m.moduleId} variant="neutral">
                                            {m.name}
                                        </Badge>
                                    ))
                                )}
                            </div>
                            {isAppAdmin(organizationId, application.applicationId) ? (
                                <ApplicationAccessManager organizationId={organizationId} application={application} />
                            ) : (
                                <p className="mt-2 text-sm text-muted-foreground">
                                    Only an administrator of {application.name} can grant access to it.
                                </p>
                            )}
                        </div>
                    ))
                )}
            </CardContent>
        </Card>
    )
}

function ApplicationAccessManager({
    organizationId,
    application,
}: {
    organizationId: number
    application: OrganizationApplication
}) {
    const applicationId = application.applicationId
    const access = useApplicationAccess(organizationId, applicationId)
    const roles = useApplicationRoles(organizationId, applicationId, false)
    const members = useOrganizationMembers(organizationId, 1, 100)
    const grant = useGrantApplicationRole()
    const revoke = useRevokeApplicationRole()
    const { toast } = useToast()
    const [userId, setUserId] = useState("")
    const [roleId, setRoleId] = useState("")
    const [expiresOn, setExpiresOn] = useState("")

    const submit = async () => {
        if (!userId || !roleId) {
            toast({ title: "Pick a member and a role", variant: "destructive" })
            return
        }
        try {
            await grant.mutateAsync({
                organizationId,
                applicationId,
                userId: Number(userId),
                roleId: Number(roleId),
                // End of the chosen day, UTC.
                expiresAt: expiresOn ? new Date(`${expiresOn}T23:59:59Z`).toISOString() : undefined,
            })
            toast({ title: "Access granted", variant: "success" })
            setUserId("")
            setRoleId("")
            setExpiresOn("")
        } catch (err) {
            toast({ title: "Could not grant access", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    const remove = async (memberId: number, revokedRoleId: number) => {
        try {
            await revoke.mutateAsync({ organizationId, applicationId, userId: memberId, roleId: revokedRoleId })
            toast({ title: "Access revoked", variant: "success" })
        } catch (err) {
            toast({ title: "Could not revoke access", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <div className="mt-3 space-y-4">
            <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Who has access</p>
                {access.isLoading ? (
                    <Skeleton className="h-12 w-full" />
                ) : !access.data || access.data.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Nobody yet.</p>
                ) : (
                    <ul className="divide-y divide-border rounded-md border border-border">
                        {access.data.map((entry) => (
                            <li key={entry.userId} className="flex flex-wrap items-center justify-between gap-2 p-3">
                                <div>
                                    <p className="text-sm font-medium text-ink-900">{entry.email}</p>
                                    <p className="text-xs text-muted-foreground">
                                        {entry.permissions.length > 0 ? entry.permissions.join(", ") : "No effective permissions"}
                                    </p>
                                </div>
                                <div className="flex flex-wrap gap-1">
                                    {entry.roles.map((r) => (
                                        <span
                                            key={r.roleId}
                                            className="inline-flex items-center gap-1 rounded-full bg-muted py-1 pl-3 pr-1 text-xs"
                                        >
                                            {r.name}
                                            {r.expiresAt && (
                                                <span className="text-muted-foreground">
                                                    · until {new Date(r.expiresAt).toLocaleDateString()}
                                                </span>
                                            )}
                                            <button
                                                type="button"
                                                aria-label={`Revoke ${r.name}`}
                                                className="flex h-7 w-7 items-center justify-center rounded-full hover:bg-red-100 hover:text-red-700"
                                                disabled={revoke.isPending}
                                                onClick={() => void remove(entry.userId, r.roleId)}
                                            >
                                                <X className="h-3.5 w-3.5" />
                                            </button>
                                        </span>
                                    ))}
                                </div>
                            </li>
                        ))}
                    </ul>
                )}
            </div>

            <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Grant access</p>
                <div className="grid gap-2 sm:grid-cols-[1.5fr_1.5fr_1fr_auto] sm:items-end">
                    <div className="grid gap-1">
                        <Label htmlFor={`grant-member-${applicationId}`} className="text-xs">Member</Label>
                        <select
                            id={`grant-member-${applicationId}`}
                            className={selectClass}
                            value={userId}
                            onChange={(e) => setUserId(e.target.value)}
                        >
                            <option value="">Select a member</option>
                            {members.data?.items.map((m) => (
                                <option key={m.userId} value={m.userId}>
                                    {m.email}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="grid gap-1">
                        <Label htmlFor={`grant-role-${applicationId}`} className="text-xs">Role</Label>
                        <select
                            id={`grant-role-${applicationId}`}
                            className={selectClass}
                            value={roleId}
                            onChange={(e) => setRoleId(e.target.value)}
                        >
                            <option value="">Select a role</option>
                            {roles.data?.map((r) => (
                                <option key={r.id} value={r.id}>
                                    {r.name}
                                    {r.organizationId === null ? "" : " (custom)"}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="grid gap-1">
                        <Label htmlFor={`grant-expiry-${applicationId}`} className="text-xs">Expires (optional)</Label>
                        <Input
                            id={`grant-expiry-${applicationId}`}
                            type="date"
                            value={expiresOn}
                            onChange={(e) => setExpiresOn(e.target.value)}
                        />
                    </div>
                    <Button onClick={() => void submit()} disabled={grant.isPending}>
                        {grant.isPending ? "Granting…" : "Grant"}
                    </Button>
                </div>
            </div>
        </div>
    )
}
