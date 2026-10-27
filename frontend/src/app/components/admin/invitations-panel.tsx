import { useState, type FormEvent } from "react"
import { ChevronLeft, ChevronRight, Copy, Loader2, MailPlus, RotateCw, Send, Trash2, X } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { EmptyState } from "../ui/empty-state"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { useToast } from "../ui/toast"
import { useAccess } from "../../hooks/useAccess"
import {
    useCreateInvitation,
    useInvitableRoles,
    useInvitations,
    useResendInvitation,
    useRevokeInvitation,
} from "../../hooks/useAdminQueries"
import { useSuppliers } from "../../hooks/useSupplierQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { formatDateTime, SELECT_CLASS } from "../../lib/admin-format"
import type { Invitation, InvitationStatus, IssuedInvitation } from "../../lib/admin-api"
import type { SupplierSummary } from "../../lib/supplier-api"

const PAGE_SIZE = 25

const STATUS_VARIANT: Record<InvitationStatus, "success" | "neutral" | "warning" | "danger"> = {
    Pending: "warning",
    Accepted: "success",
    Revoked: "neutral",
    Expired: "danger",
}

/** Invitations tab (users.invite or users.view): invite external users and manage their invitations. */
export function InvitationsPanel() {
    const { can } = useAccess()
    const canInvite = can("users.invite")
    // Accept links are only returned by create and resend; remember them for "Copy link".
    const [links, setLinks] = useState<Record<number, string>>({})

    const remember = (issued: IssuedInvitation) =>
        setLinks((current) => ({ ...current, [issued.invitation.id]: issued.acceptLink }))

    return (
        <div className="space-y-6">
            {canInvite && <InviteForm onIssued={remember} />}
            <InvitationsTable links={links} canInvite={canInvite} onIssued={remember} />
        </div>
    )
}

function InviteForm({ onIssued }: { onIssued: (issued: IssuedInvitation) => void }) {
    const { toast } = useToast()
    const roles = useInvitableRoles()
    const create = useCreateInvitation()
    const [email, setEmail] = useState("")
    const [roleId, setRoleId] = useState<number | "">("")
    const [suppliers, setSuppliers] = useState<SupplierSummary[]>([])
    const [lastLink, setLastLink] = useState<string | null>(null)

    const chosenRoleId = roleId || roles.data?.[0]?.id

    const submit = async (e: FormEvent) => {
        e.preventDefault()
        if (!chosenRoleId || suppliers.length === 0) {
            return
        }

        try {
            const issued = await create.mutateAsync({
                email: email.trim(),
                roleId: chosenRoleId,
                supplierRecordIds: suppliers.map((supplier) => supplier.recordId),
            })
            onIssued(issued)
            setLastLink(issued.acceptLink)
            setEmail("")
            setSuppliers([])
            toast(
                issued.emailSent
                    ? { title: "Invitation sent", description: `Emailed to ${issued.invitation.email}.`, variant: "success" }
                    : {
                          title: "Invitation created, but the email couldn't be sent",
                          description: "Copy the link and send it to them.",
                          variant: "destructive",
                      }
            )
        } catch (err) {
            toast({ title: "Could not create the invitation", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Card>
            <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                    <MailPlus className="h-5 w-5 text-brand-600" />
                    Invite an external user
                </CardTitle>
            </CardHeader>
            <CardContent>
                <form onSubmit={submit} className="grid gap-4 lg:grid-cols-2">
                    <div className="space-y-2">
                        <Label htmlFor="invite-email">Email</Label>
                        <Input id="invite-email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
                    </div>
                    <div className="space-y-2">
                        <Label htmlFor="invite-role">Role</Label>
                        <select
                            id="invite-role"
                            className={`${SELECT_CLASS} w-full`}
                            value={chosenRoleId ?? ""}
                            onChange={(e) => setRoleId(Number(e.target.value))}
                            disabled={!roles.data}
                        >
                            {(roles.data ?? []).map((role) => (
                                <option key={role.id} value={role.id}>
                                    {role.name}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div className="space-y-2 lg:col-span-2">
                        <Label>Suppliers</Label>
                        <SupplierPicker
                            selected={suppliers}
                            onAdd={(supplier) => setSuppliers((current) => [...current, supplier])}
                            onRemove={(recordId) => setSuppliers((current) => current.filter((s) => s.recordId !== recordId))}
                        />
                    </div>
                    <div className="lg:col-span-2">
                        <Button type="submit" size="lg" disabled={create.isPending || suppliers.length === 0 || !chosenRoleId}>
                            {create.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                            Create invitation
                        </Button>
                    </div>
                </form>
                {lastLink && <IssuedLink link={lastLink} />}
            </CardContent>
        </Card>
    )
}

/** Search suppliers by name and collect their Record IDs. */
function SupplierPicker({
    selected,
    onAdd,
    onRemove,
}: {
    selected: SupplierSummary[]
    onAdd: (supplier: SupplierSummary) => void
    onRemove: (recordId: number) => void
}) {
    const [term, setTerm] = useState("")
    const search = term.trim()
    const results = useSuppliers(search, 0, 8, search.length >= 2)
    const chosen = new Set(selected.map((s) => s.recordId))

    return (
        <div className="space-y-2">
            {selected.length > 0 && (
                <div className="flex flex-wrap gap-2">
                    {selected.map((supplier) => (
                        <span key={supplier.recordId} className="inline-flex items-center gap-1 rounded-full bg-brand-50 py-1 pl-3 pr-1 text-sm text-brand-700">
                            {supplier.account || `#${supplier.recordId}`}
                            <button
                                type="button"
                                className="rounded-full p-1 hover:bg-brand-100"
                                aria-label={`Remove ${supplier.account}`}
                                onClick={() => onRemove(supplier.recordId)}
                            >
                                <X className="h-3.5 w-3.5" />
                            </button>
                        </span>
                    ))}
                </div>
            )}
            <Input placeholder="Search suppliers by name…" value={term} onChange={(e) => setTerm(e.target.value)} aria-label="Search suppliers" />
            {search.length >= 2 && (
                <ul className="max-h-56 divide-y divide-border overflow-y-auto rounded-lg border border-border">
                    {results.isLoading ? (
                        <li className="px-3 py-2 text-sm text-muted-foreground">Searching…</li>
                    ) : results.isError ? (
                        <li className="px-3 py-2 text-sm text-red-600">{adminErrorMessage(results.error)}</li>
                    ) : (results.data?.items ?? []).length === 0 ? (
                        <li className="px-3 py-2 text-sm text-muted-foreground">No suppliers match.</li>
                    ) : (
                        results.data!.items.map((supplier) => (
                            <li key={supplier.recordId}>
                                <button
                                    type="button"
                                    disabled={chosen.has(supplier.recordId)}
                                    className="flex min-h-11 w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-muted disabled:opacity-50"
                                    onClick={() => {
                                        onAdd(supplier)
                                        setTerm("")
                                    }}
                                >
                                    <span className="font-medium text-ink-900">{supplier.account}</span>
                                    <span className="text-xs text-muted-foreground">#{supplier.recordId}</span>
                                </button>
                            </li>
                        ))
                    )}
                </ul>
            )}
        </div>
    )
}

function IssuedLink({ link }: { link: string }) {
    return (
        <div className="mt-4 flex flex-col gap-2 rounded-lg border border-brand-200 bg-brand-50 p-3 sm:flex-row sm:items-center">
            <code className="min-w-0 flex-1 truncate text-xs text-brand-800">{link}</code>
            <CopyLinkButton link={link} />
        </div>
    )
}

function CopyLinkButton({ link }: { link: string }) {
    const { toast } = useToast()

    const copy = async () => {
        try {
            await navigator.clipboard.writeText(link)
            toast({ title: "Link copied", variant: "success" })
        } catch {
            toast({ title: "Could not copy the link", description: link, variant: "destructive" })
        }
    }

    return (
        <Button type="button" variant="outline" size="sm" onClick={() => void copy()}>
            <Copy className="h-4 w-4" />
            Copy link
        </Button>
    )
}

function InvitationsTable({
    links,
    canInvite,
    onIssued,
}: {
    links: Record<number, string>
    canInvite: boolean
    onIssued: (issued: IssuedInvitation) => void
}) {
    const { toast } = useToast()
    const [page, setPage] = useState(1)
    const { data, isLoading, isError, error } = useInvitations(page, PAGE_SIZE)
    const resend = useResendInvitation()
    const revoke = useRevokeInvitation()

    const onResend = async (invitation: Invitation) => {
        try {
            const issued = await resend.mutateAsync(invitation.id)
            onIssued(issued)
            toast(
                issued.emailSent
                    ? { title: "Invitation resent", description: `A new link was emailed to ${invitation.email}; the old one no longer works.`, variant: "success" }
                    : {
                          title: "New link issued, but the email couldn't be sent",
                          description: "The old link no longer works. Copy the new one and send it.",
                          variant: "destructive",
                      }
            )
        } catch (err) {
            toast({ title: "Could not resend", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    const onRevoke = async (invitation: Invitation) => {
        try {
            await revoke.mutateAsync(invitation.id)
            toast({ title: "Invitation revoked", variant: "success" })
        } catch (err) {
            toast({ title: "Could not revoke", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    if (isLoading) {
        return <Skeleton className="h-40 w-full" />
    }

    if (isError) {
        return <EmptyState icon={MailPlus} title="Could not load invitations" description={adminErrorMessage(error)} />
    }

    if (!data || data.items.length === 0) {
        return <EmptyState icon={MailPlus} title="No invitations yet" description="Invitations you create appear here." />
    }

    return (
        <div className="space-y-3">
            <div className="overflow-x-auto rounded-xl border border-border bg-card">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Email</TableHead>
                            <TableHead>Role</TableHead>
                            <TableHead>Suppliers</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Expires</TableHead>
                            {canInvite && <TableHead className="text-right">Actions</TableHead>}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {data.items.map((invitation) => {
                            const open = invitation.status === "Pending" || invitation.status === "Expired"
                            return (
                                <TableRow key={invitation.id}>
                                    <TableCell className="font-medium text-ink-900">{invitation.email}</TableCell>
                                    <TableCell>{invitation.roleKey}</TableCell>
                                    <TableCell className="text-muted-foreground">
                                        {invitation.supplierRecordIds.map((id) => `#${id}`).join(", ")}
                                    </TableCell>
                                    <TableCell>
                                        <Badge variant={STATUS_VARIANT[invitation.status]}>{invitation.status}</Badge>
                                    </TableCell>
                                    <TableCell className="whitespace-nowrap text-muted-foreground">{formatDateTime(invitation.expiresAt)}</TableCell>
                                    {canInvite && (
                                        <TableCell>
                                            <div className="flex justify-end gap-2">
                                                {links[invitation.id] && invitation.status === "Pending" && <CopyLinkButton link={links[invitation.id]} />}
                                                {open && (
                                                    <>
                                                        <Button variant="outline" size="sm" disabled={resend.isPending} onClick={() => void onResend(invitation)}>
                                                            <RotateCw className="h-4 w-4" />
                                                            Resend
                                                        </Button>
                                                        <Button variant="ghost" size="sm" disabled={revoke.isPending} onClick={() => void onRevoke(invitation)}>
                                                            <Trash2 className="h-4 w-4" />
                                                            Revoke
                                                        </Button>
                                                    </>
                                                )}
                                            </div>
                                        </TableCell>
                                    )}
                                </TableRow>
                            )
                        })}
                    </TableBody>
                </Table>
            </div>
            <div className="flex items-center justify-end gap-2 text-sm text-muted-foreground">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    <ChevronLeft className="h-4 w-4" />
                    Previous
                </Button>
                <span>
                    Page {data.page} of {Math.max(data.totalPages, 1)}
                </span>
                <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
                    Next
                    <ChevronRight className="h-4 w-4" />
                </Button>
            </div>
        </div>
    )
}
