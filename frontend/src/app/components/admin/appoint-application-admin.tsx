import { useState } from "react"

import { Button } from "../ui/button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { useToast } from "../ui/toast"
import { useApplicationRoles, useGrantApplicationRole } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

interface Props {
    organizationId: number
    applicationId: number
    applicationName: string
}

const selectClass =
    "h-11 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"

/**
 * Platform administrators: grant an application role to an organization
 * member, typically "{App} Administrator" to appoint its first application
 * administrator, who then grants everyone else from the organization page.
 * The user must already be a member of the organization.
 */
export function AppointApplicationAdmin({ organizationId, applicationId, applicationName }: Props) {
    const roles = useApplicationRoles(organizationId, applicationId, true)
    const grant = useGrantApplicationRole()
    const { toast } = useToast()
    const [userId, setUserId] = useState("")
    const [roleId, setRoleId] = useState("")

    const administrator = roles.data?.find((r) => r.name === `${applicationName} Administrator`)
    const selectedRoleId = roleId || (administrator ? String(administrator.id) : "")

    const submit = async () => {
        if (!/^[1-9][0-9]*$/.test(userId.trim()) || !selectedRoleId) {
            toast({ title: "Enter a user id and pick a role", variant: "destructive" })
            return
        }
        try {
            await grant.mutateAsync({
                organizationId,
                applicationId,
                userId: Number(userId.trim()),
                roleId: Number(selectedRoleId),
                asPlatformAdmin: true,
            })
            toast({ title: "Role granted", description: `User ${userId.trim()} now holds it in ${applicationName}.`, variant: "success" })
            setUserId("")
        } catch (err) {
            toast({ title: "Could not grant the role", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <div className="mt-3 space-y-2 border-t border-border pt-3">
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Appoint an application administrator
            </p>
            <div className="grid gap-2 sm:grid-cols-[1fr_1.5fr_auto] sm:items-end">
                <div className="grid gap-1">
                    <Label htmlFor={`appoint-user-${applicationId}`} className="text-xs">Member user id</Label>
                    <Input
                        id={`appoint-user-${applicationId}`}
                        inputMode="numeric"
                        placeholder="42"
                        value={userId}
                        onChange={(e) => setUserId(e.target.value)}
                    />
                </div>
                <div className="grid gap-1">
                    <Label htmlFor={`appoint-role-${applicationId}`} className="text-xs">Role</Label>
                    <select
                        id={`appoint-role-${applicationId}`}
                        className={selectClass}
                        value={selectedRoleId}
                        onChange={(e) => setRoleId(e.target.value)}
                    >
                        <option value="">{roles.isLoading ? "Loading…" : "Select a role"}</option>
                        {roles.data?.map((r) => (
                            <option key={r.id} value={r.id}>
                                {r.name}
                            </option>
                        ))}
                    </select>
                </div>
                <Button onClick={() => void submit()} disabled={grant.isPending}>
                    {grant.isPending ? "Granting…" : "Grant"}
                </Button>
            </div>
            <p className="text-xs text-muted-foreground">
                The user must already be a member of this organization (find ids on the Users tab).
            </p>
        </div>
    )
}
