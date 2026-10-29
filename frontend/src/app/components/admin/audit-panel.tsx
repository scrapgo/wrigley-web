import { useState, type FormEvent } from "react"
import { ChevronLeft, ChevronRight, Filter, ScrollText, Search, X } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { EmptyState } from "../ui/empty-state"
import { HelpButton } from "../ui/help-button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { useAccess } from "../../hooks/useAccess"
import { useAudit, useUsers } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { formatDateTime } from "../../lib/admin-format"
import { cn } from "../../lib/utils"
import type { AuditEntry, AuditQuery } from "../../lib/admin-api"

const PAGE_SIZE = 25

type Filters = Omit<AuditQuery, "page" | "pageSize">

/** The actions admins look for most often; one click fills the Action filter. */
const QUICK_FILTERS: { label: string; action: string }[] = [
    { label: "Invited someone", action: "invitation.created" },
    { label: "Accepted an invite", action: "invitation.accepted" },
    { label: "Disabled a user", action: "user.disabled" },
    { label: "Enabled a user", action: "user.enabled" },
    { label: "Changed roles", action: "user.roles_changed" },
    { label: "Changed permissions", action: "role.permissions_changed" },
    { label: "Access denied", action: "access.denied" },
]

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
    const { can } = useAccess()
    const canSearchActors = can("users.view")
    const [action, setAction] = useState("")
    const [actorId, setActorId] = useState<number | undefined>(undefined)
    const [actorLabel, setActorLabel] = useState("")
    const [from, setFrom] = useState("")
    const [to, setTo] = useState("")
    const [filters, setFilters] = useState<Filters>({})
    const [page, setPage] = useState(1)

    const { data, isLoading, isError, error, isFetching } = useAudit({ ...filters, page, pageSize: PAGE_SIZE })

    const applyFilters = (overrideAction?: string) => {
        setPage(1)
        setFilters({
            action: (overrideAction ?? action).trim() || undefined,
            actor: actorId,
            from: from ? startOfDay(from) : undefined,
            to: to ? endOfDay(to) : undefined,
        })
    }

    const apply = (e: FormEvent) => {
        e.preventDefault()
        applyFilters()
    }

    const quickFilter = (quickAction: string) => {
        setAction(quickAction)
        applyFilters(quickAction)
    }

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between gap-2">
                <div className="flex flex-wrap gap-2">
                    {QUICK_FILTERS.map((filter) => (
                        <button
                            key={filter.action}
                            type="button"
                            onClick={() => quickFilter(filter.action)}
                            className={cn(
                                "min-h-9 rounded-full border px-3 text-sm font-medium transition-colors",
                                action === filter.action
                                    ? "border-brand-500 bg-brand-50 text-brand-700"
                                    : "border-border text-ink-700 hover:bg-muted"
                            )}
                        >
                            {filter.label}
                        </button>
                    ))}
                </div>
                <HelpButton
                    title="Audit log"
                    description="A chronological record of what happened in ScrapGo: who invited or disabled a user, changed roles or permissions, turned features on or off, and when access was denied. Use the quick filters or search by action, actor, and date range."
                />
            </div>

            <form onSubmit={apply} className="grid gap-3 rounded-xl border border-border bg-card p-4 sm:grid-cols-2 lg:grid-cols-5">
                <div className="space-y-1">
                    <Label htmlFor="audit-action">Action</Label>
                    <Input id="audit-action" placeholder="e.g. user.disabled" value={action} onChange={(e) => setAction(e.target.value)} />
                </div>
                <div className="space-y-1">
                    <Label htmlFor="audit-actor">Actor</Label>
                    {canSearchActors ? (
                        <ActorPicker
                            selectedLabel={actorLabel}
                            onSelect={(id, label) => {
                                setActorId(id)
                                setActorLabel(label)
                            }}
                            onClear={() => {
                                setActorId(undefined)
                                setActorLabel("")
                            }}
                        />
                    ) : (
                        <Input
                            id="audit-actor"
                            inputMode="numeric"
                            placeholder="User ID"
                            value={actorId ?? ""}
                            onChange={(e) => setActorId(e.target.value ? Number(e.target.value.replace(/\D/g, "")) : undefined)}
                        />
                    )}
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

/** Searches users by name or email and reports the chosen id, instead of asking for a raw user ID. */
function ActorPicker({
    selectedLabel,
    onSelect,
    onClear,
}: {
    selectedLabel: string
    onSelect: (id: number, label: string) => void
    onClear: () => void
}) {
    const [term, setTerm] = useState("")
    const search = term.trim()
    const results = useUsers({ search: search || undefined, page: 1, pageSize: 6 })

    if (selectedLabel) {
        return (
            <div className="flex min-h-10 items-center justify-between gap-2 rounded-lg border border-border bg-muted/40 px-3 text-sm">
                <span className="truncate font-medium text-ink-900">{selectedLabel}</span>
                <button type="button" onClick={onClear} aria-label="Clear actor filter" className="rounded-full p-1 hover:bg-muted">
                    <X className="h-3.5 w-3.5" />
                </button>
            </div>
        )
    }

    return (
        <div className="relative">
            <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="audit-actor" className="pl-9" placeholder="Search by name or email…" value={term} onChange={(e) => setTerm(e.target.value)} />
            </div>
            {search.length >= 2 && (
                <ul className="absolute z-10 mt-1 max-h-48 w-full divide-y divide-border overflow-y-auto rounded-lg border border-border bg-popover shadow-elevated">
                    {results.isLoading ? (
                        <li className="px-3 py-2 text-sm text-muted-foreground">Searching…</li>
                    ) : results.isError ? (
                        <li className="px-3 py-2 text-sm text-red-600">{adminErrorMessage(results.error)}</li>
                    ) : (results.data?.items ?? []).length === 0 ? (
                        <li className="px-3 py-2 text-sm text-muted-foreground">No one matches.</li>
                    ) : (
                        (results.data?.items ?? []).map((user) => (
                            <li key={user.id}>
                                <button
                                    type="button"
                                    className="flex min-h-11 w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-muted"
                                    onClick={() => {
                                        onSelect(user.id, user.displayName || user.email)
                                        setTerm("")
                                    }}
                                >
                                    <span className="font-medium text-ink-900">{user.displayName || user.email}</span>
                                    <span className="text-xs text-muted-foreground">{user.email}</span>
                                </button>
                            </li>
                        ))
                    )}
                </ul>
            )}
        </div>
    )
}
