import { useState } from "react"
import { Link } from "@tanstack/react-router"
import { ArrowLeft, Building2, Loader2, Plus, ShieldCheck, UserCheck, UserRound, UserX, X } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { Checkbox } from "../ui/checkbox"
import { EmptyState } from "../ui/empty-state"
import { HelpButton } from "../ui/help-button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { useToast } from "../ui/toast"
import { Can } from "../access"
import { useAccess } from "../../hooks/useAccess"
import {
    useDisableUser,
    useEnableUser,
    useRoles,
    useSetUserRoles,
    useSetUserSuppliers,
    useUserDetail,
} from "../../hooks/useAdminQueries"
import { useSupplier } from "../../hooks/useSupplierQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { formatDateTime } from "../../lib/admin-format"
import type { UserDetail } from "../../lib/admin-api"

const MANAGE = ["users.manage"]

/** One user (users.view): identity, roles, supplier links and status; changes need users.manage. */
export function UserDetailPanel({ userId }: { userId: number }) {
    const { data: user, isLoading, isError, error } = useUserDetail(userId)

    if (isLoading) {
        return <Skeleton className="h-64 w-full" />
    }

    if (isError || !user) {
        return <EmptyState icon={UserRound} title="Could not load this user" description={adminErrorMessage(error)} />
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between gap-2">
                <Button variant="ghost" asChild>
                    <Link to="/admin" search={{ tab: "users" }}>
                        <ArrowLeft className="h-4 w-4" />
                        All users
                    </Link>
                </Button>
                <HelpButton
                    title="User details"
                    description="This user's identity, status, roles, and supplier links. Use Disable to block sign-in without deleting the account. Roles control what the user can do; supplier links (external users only) control which suppliers they can see."
                />
            </div>

            <ProfileCard user={user} />
            {/* Remount when the saved roles change, so the checkboxes start from them. */}
            <RolesCard key={user.roles.map((role) => role.id).join(",")} user={user} />
            {user.userType === "External" && <SuppliersCard user={user} />}
        </div>
    )
}

function ProfileCard({ user }: { user: UserDetail }) {
    const { toast } = useToast()
    const disable = useDisableUser()
    const enable = useEnableUser()
    const isActive = user.status === "Active"
    const pending = disable.isPending || enable.isPending

    const toggle = async () => {
        try {
            await (isActive ? disable : enable).mutateAsync(user.id)
            toast({ title: isActive ? "User disabled" : "User enabled", variant: "success" })
        } catch (err) {
            toast({ title: isActive ? "Could not disable user" : "Could not enable user", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Card>
            <CardHeader className="flex flex-row items-start justify-between gap-4">
                <CardTitle className="flex items-center gap-2 text-lg">
                    <UserRound className="h-5 w-5 text-brand-600" />
                    {user.displayName || user.email}
                </CardTitle>
                <Can anyOf={MANAGE}>
                    <Button variant={isActive ? "outline" : "default"} size="lg" disabled={pending} onClick={() => void toggle()}>
                        {pending ? <Loader2 className="h-4 w-4 animate-spin" /> : isActive ? <UserX className="h-4 w-4" /> : <UserCheck className="h-4 w-4" />}
                        {isActive ? "Disable" : "Enable"}
                    </Button>
                </Can>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm sm:grid-cols-2">
                <Detail label="Email" value={user.email} />
                <Detail label="Type" value={<Badge variant={user.userType === "Internal" ? "success" : "neutral"}>{user.userType}</Badge>} />
                <Detail label="Status" value={<Badge variant={isActive ? "success" : "danger"}>{user.status}</Badge>} />
                <Detail label="Last sign-in" value={formatDateTime(user.lastSignInAt)} />
                <Detail label="Sign-in method" value={user.signInProvider ?? "—"} />
                <Detail label="Created" value={formatDateTime(user.createdAt)} />
            </CardContent>
        </Card>
    )
}

function RolesCard({ user }: { user: UserDetail }) {
    const { can } = useAccess()
    const canManage = can(...MANAGE)
    const { toast } = useToast()
    const roles = useRoles(canManage)
    const save = useSetUserRoles()
    const [selected, setSelected] = useState(() => new Set(user.roles.map((role) => role.id)))

    // Only roles of the user's own type can be held (the API refuses others).
    const choices = (roles.data ?? []).filter((role) => role.userType === user.userType)
    const changed =
        selected.size !== user.roles.length || user.roles.some((role) => !selected.has(role.id))

    const toggle = (id: number, checked: boolean) =>
        setSelected((current) => {
            const next = new Set(current)
            if (checked) next.add(id)
            else next.delete(id)
            return next
        })

    const onSave = async () => {
        try {
            await save.mutateAsync({ id: user.id, roleIds: [...selected] })
            toast({ title: "Roles saved", variant: "success" })
        } catch (err) {
            toast({ title: "Could not save roles", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Card>
            <CardHeader className="flex items-center justify-between gap-2">
                <CardTitle className="flex items-center gap-2 text-lg">
                    <ShieldCheck className="h-5 w-5 text-brand-600" />
                    Roles
                </CardTitle>
                <HelpButton
                    title="Roles"
                    description="Roles control what this user can do in ScrapGo. Check a role to grant it; uncheck to remove it. Only roles matching this user's type (Internal or External) are shown. Click Save roles when you're done."
                />
            </CardHeader>
            <CardContent className="space-y-4">
                {!canManage ? (
                    <div className="flex flex-wrap gap-2">
                        {user.roles.length === 0 ? (
                            <span className="text-sm text-muted-foreground">No roles.</span>
                        ) : (
                            user.roles.map((role) => (
                                <Badge key={role.id} variant="neutral">
                                    {role.name}
                                </Badge>
                            ))
                        )}
                    </div>
                ) : roles.isLoading ? (
                    <Skeleton className="h-16 w-full" />
                ) : roles.isError ? (
                    <p className="text-sm text-red-600">{adminErrorMessage(roles.error)}</p>
                ) : (
                    <>
                        <div className="space-y-3">
                            {choices.map((role) => (
                                <label key={role.id} className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border border-border p-3">
                                    <Checkbox
                                        checked={selected.has(role.id)}
                                        onCheckedChange={(checked) => toggle(role.id, checked === true)}
                                        aria-label={role.name}
                                    />
                                    <span className="leading-tight">
                                        <span className="block text-sm font-medium text-ink-900">{role.name}</span>
                                        {role.description && <span className="block text-xs text-muted-foreground">{role.description}</span>}
                                    </span>
                                </label>
                            ))}
                        </div>
                        <Button size="lg" disabled={!changed || save.isPending} onClick={() => void onSave()}>
                            {save.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                            Save roles
                        </Button>
                    </>
                )}
            </CardContent>
        </Card>
    )
}

function SuppliersCard({ user }: { user: UserDetail }) {
    const { can } = useAccess()
    const canManage = can(...MANAGE)
    const { toast } = useToast()
    const save = useSetUserSuppliers()
    const [input, setInput] = useState("")
    const candidate = Number.parseInt(input, 10)
    const validCandidate = Number.isInteger(candidate) && candidate > 0 && !user.supplierRecordIds.includes(candidate)
    const lookup = useSupplier(validCandidate ? candidate : 0)

    const saveLinks = async (supplierRecordIds: number[], success: string) => {
        try {
            await save.mutateAsync({ id: user.id, supplierRecordIds })
            toast({ title: success, variant: "success" })
            setInput("")
        } catch (err) {
            toast({ title: "Could not save supplier links", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Card>
            <CardHeader className="flex items-center justify-between gap-2">
                <CardTitle className="flex items-center gap-2 text-lg">
                    <Building2 className="h-5 w-5 text-brand-600" />
                    Supplier links
                </CardTitle>
                <HelpButton
                    title="Supplier links"
                    description="External users only see the suppliers you link here. Type a supplier Record ID to add one, or click X next to a supplier to remove it. This controls which suppliers appear in that user's workspace."
                />
            </CardHeader>
            <CardContent className="space-y-4">
                <p className="text-sm text-muted-foreground">This user sees only these suppliers.</p>
                {user.supplierRecordIds.length === 0 ? (
                    <p className="text-sm text-muted-foreground">No suppliers linked.</p>
                ) : (
                    <ul className="divide-y divide-border rounded-lg border border-border">
                        {user.supplierRecordIds.map((id) => (
                            <li key={id} className="flex min-h-11 items-center justify-between gap-3 px-3 py-2 text-sm">
                                <span>
                                    <SupplierName recordId={id} />
                                    <span className="ml-2 text-xs text-muted-foreground">#{id}</span>
                                </span>
                                {canManage && (
                                    <Button
                                        variant="ghost"
                                        size="icon"
                                        aria-label={`Unlink supplier ${id}`}
                                        disabled={save.isPending}
                                        onClick={() => void saveLinks(user.supplierRecordIds.filter((other) => other !== id), "Supplier unlinked")}
                                    >
                                        <X className="h-4 w-4" />
                                    </Button>
                                )}
                            </li>
                        ))}
                    </ul>
                )}

                {canManage && (
                    <form
                        className="space-y-2"
                        onSubmit={(e) => {
                            e.preventDefault()
                            if (validCandidate && lookup.data) {
                                void saveLinks([...user.supplierRecordIds, candidate], "Supplier linked")
                            }
                        }}
                    >
                        <Label htmlFor="supplier-record-id">Add a supplier by Record ID</Label>
                        <div className="flex gap-2">
                            <Input
                                id="supplier-record-id"
                                inputMode="numeric"
                                placeholder="e.g. 17511"
                                value={input}
                                onChange={(e) => setInput(e.target.value.replace(/\D/g, ""))}
                            />
                            <Button type="submit" size="lg" disabled={!validCandidate || !lookup.data || save.isPending}>
                                <Plus className="h-4 w-4" />
                                Add
                            </Button>
                        </div>
                        {validCandidate && (
                            <p className="text-sm text-muted-foreground">
                                {lookup.isLoading
                                    ? "Looking up…"
                                    : lookup.data
                                      ? lookup.data.supplier.account
                                      : lookup.isError
                                        ? adminErrorMessage(lookup.error)
                                        : null}
                            </p>
                        )}
                    </form>
                )}
            </CardContent>
        </Card>
    )
}

function SupplierName({ recordId }: { recordId: number }) {
    const { data, isLoading } = useSupplier(recordId)
    return <span className="font-medium text-ink-900">{isLoading ? "…" : (data?.supplier.account ?? "Unknown supplier")}</span>
}

function Detail({ label, value }: { label: string; value: React.ReactNode }) {
    return (
        <div className="flex items-center justify-between gap-4 border-b border-border pb-2 sm:block sm:border-0 sm:pb-0">
            <span className="block text-xs uppercase tracking-wide text-muted-foreground">{label}</span>
            <span className="block font-medium text-ink-900 sm:mt-0.5">{value}</span>
        </div>
    )
}
