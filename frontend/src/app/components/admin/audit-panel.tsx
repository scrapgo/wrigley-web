import { useState, type FormEvent } from "react"
import { ChevronLeft, ChevronRight, Filter, ScrollText } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { EmptyState } from "../ui/empty-state"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { useAudit } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { formatDateTime } from "../../lib/admin-format"
import type { AuditEntry, AuditQuery } from "../../lib/admin-api"

const PAGE_SIZE = 25

type Filters = Omit<AuditQuery, "page" | "pageSize">

function actorOf(entry: AuditEntry) {
    if (entry.actorEmail) return entry.actorEmail
    if (entry.actorUserId) return `User #${entry.actorUserId}`
    return entry.actorType
}

/** What the event was about, from the row's metadata. */
function targetOf(entry: AuditEntry) {
    const m = entry.metadata ?? {}
    if (typeof m.targetUserId === "number") return `User #${m.targetUserId}`
    if (typeof m.grantedUserId === "number") return `User #${m.grantedUserId}`
    if (typeof m.roleKey === "string") return `Role ${m.roleKey}`
    if (typeof m.featureKey === "string") return `Feature ${m.featureKey}`
    if (typeof m.email === "string") return m.email
    if (typeof m.invitationId === "number") return `Invitation #${m.invitationId}`
    if (typeof m.path === "string") return m.path
    return "—"
}

/** Audit rows record what happened; only access.denied rows are refusals. */
function resultOf(entry: AuditEntry) {
    if (entry.action !== "access.denied") {
        return <Badge variant="success">Success</Badge>
    }
    const reason = typeof entry.metadata?.reason === "string" ? entry.metadata.reason : null
    return <Badge variant="danger">{reason ? `Denied · ${reason}` : "Denied"}</Badge>
}

/** A local calendar date (yyyy-mm-dd) as the ISO instant of its local midnight. */
function startOfDay(date: string) {
    return new Date(`${date}T00:00:00`).toISOString()
}

/** The exclusive upper bound for an inclusive "to" date: the next local midnight. */
function endOfDay(date: string) {
    const next = new Date(`${date}T00:00:00`)
    next.setDate(next.getDate() + 1)
    return next.toISOString()
}

/** Audit log tab (audit.view): read-only, newest first, with filters. */
export function AuditPanel() {
    const [action, setAction] = useState("")
    const [actor, setActor] = useState("")
    const [from, setFrom] = useState("")
    const [to, setTo] = useState("")
    const [filters, setFilters] = useState<Filters>({})
    const [page, setPage] = useState(1)

    const { data, isLoading, isError, error, isFetching } = useAudit({ ...filters, page, pageSize: PAGE_SIZE })

    const apply = (e: FormEvent) => {
        e.preventDefault()
        const actorId = Number.parseInt(actor, 10)
        setPage(1)
        setFilters({
            action: action.trim() || undefined,
            actor: Number.isInteger(actorId) && actorId > 0 ? actorId : undefined,
            from: from ? startOfDay(from) : undefined,
            to: to ? endOfDay(to) : undefined,
        })
    }

    return (
        <div className="space-y-4">
            <form onSubmit={apply} className="grid gap-3 rounded-xl border border-border bg-card p-4 sm:grid-cols-2 lg:grid-cols-5">
                <div className="space-y-1">
                    <Label htmlFor="audit-action">Action</Label>
                    <Input id="audit-action" placeholder="e.g. user.disabled" value={action} onChange={(e) => setAction(e.target.value)} />
                </div>
                <div className="space-y-1">
                    <Label htmlFor="audit-actor">Actor user ID</Label>
                    <Input id="audit-actor" inputMode="numeric" value={actor} onChange={(e) => setActor(e.target.value.replace(/\D/g, ""))} />
                </div>
                <div className="space-y-1">
                    <Label htmlFor="audit-from">From</Label>
                    <Input id="audit-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
                </div>
                <div className="space-y-1">
                    <Label htmlFor="audit-to">To</Label>
                    <Input id="audit-to" type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
                </div>
                <div className="flex items-end">
                    <Button type="submit" size="lg" variant="outline" className="w-full">
                        <Filter className="h-4 w-4" />
                        Apply
                    </Button>
                </div>
            </form>

            {isLoading ? (
                <Skeleton className="h-64 w-full" />
            ) : isError ? (
                <EmptyState icon={ScrollText} title="Could not load the audit log" description={adminErrorMessage(error)} />
            ) : !data || data.items.length === 0 ? (
                <EmptyState icon={ScrollText} title="No audit entries" description="Nothing matches these filters." />
            ) : (
                <>
                    <div className="overflow-x-auto rounded-xl border border-border bg-card">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Time</TableHead>
                                    <TableHead>Actor</TableHead>
                                    <TableHead>Action</TableHead>
                                    <TableHead>Target</TableHead>
                                    <TableHead>Result</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {data.items.map((entry) => (
                                    <TableRow key={entry.id}>
                                        <TableCell className="whitespace-nowrap text-muted-foreground">{formatDateTime(entry.eventTime)}</TableCell>
                                        <TableCell>{actorOf(entry)}</TableCell>
                                        <TableCell className="font-mono text-xs">{entry.action}</TableCell>
                                        <TableCell>{targetOf(entry)}</TableCell>
                                        <TableCell>{resultOf(entry)}</TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </div>
                    <div className="flex items-center justify-between text-sm text-muted-foreground">
                        <span>
                            {data.totalCount} entr{data.totalCount === 1 ? "y" : "ies"}
                            {isFetching && " · updating…"}
                        </span>
                        <div className="flex items-center gap-2">
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
                </>
            )}
        </div>
    )
}
