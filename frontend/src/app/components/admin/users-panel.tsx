import { useState, type FormEvent } from "react"
import { Link } from "@tanstack/react-router"
import { ChevronLeft, ChevronRight, Search, Users } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Input } from "../ui/input"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { useUsers } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import type { UserStatus, UserType } from "../../lib/admin-api"
import { formatDateTime, SELECT_CLASS } from "../../lib/admin-format"

const PAGE_SIZE = 25

/** Users tab (users.view): search, type and status filters, and a paged table. */
export function UsersPanel() {
    const [searchInput, setSearchInput] = useState("")
    const [search, setSearch] = useState("")
    const [type, setType] = useState<UserType | "">("")
    const [status, setStatus] = useState<UserStatus | "">("")
    const [page, setPage] = useState(1)

    const { data, isLoading, isError, error, isFetching } = useUsers({
        search: search || undefined,
        type: type || undefined,
        status: status || undefined,
        page,
        pageSize: PAGE_SIZE,
    })

    const onSearch = (e: FormEvent) => {
        e.preventDefault()
        setPage(1)
        setSearch(searchInput.trim())
    }

    return (
        <div className="space-y-4">
            <form onSubmit={onSearch} className="flex flex-col gap-2 sm:flex-row">
                <Input
                    className="flex-1"
                    placeholder="Search by name or email…"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    aria-label="Search users"
                />
                <select
                    className={SELECT_CLASS}
                    value={type}
                    aria-label="Filter by type"
                    onChange={(e) => {
                        setPage(1)
                        setType(e.target.value as UserType | "")
                    }}
                >
                    <option value="">All types</option>
                    <option value="Internal">Internal</option>
                    <option value="External">External</option>
                </select>
                <select
                    className={SELECT_CLASS}
                    value={status}
                    aria-label="Filter by status"
                    onChange={(e) => {
                        setPage(1)
                        setStatus(e.target.value as UserStatus | "")
                    }}
                >
                    <option value="">All statuses</option>
                    <option value="Active">Active</option>
                    <option value="Disabled">Disabled</option>
                </select>
                <Button type="submit" variant="outline" size="lg">
                    <Search className="h-4 w-4" />
                    Search
                </Button>
            </form>

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState icon={Users} title="Could not load users" description={adminErrorMessage(error)} />
            ) : !data || data.items.length === 0 ? (
                <EmptyState icon={Users} title="No users found" description="Try another search or filter." />
            ) : (
                <>
                    <div className="overflow-x-auto rounded-xl border border-border bg-card">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Name</TableHead>
                                    <TableHead>Email</TableHead>
                                    <TableHead>Type</TableHead>
                                    <TableHead>Roles</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead>Last sign-in</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {data.items.map((user) => (
                                    <TableRow key={user.id}>
                                        <TableCell className="font-medium text-ink-900">
                                            <Link
                                                to="/admin/users/$userId"
                                                params={{ userId: String(user.id) }}
                                                className="hover:text-brand-600 hover:underline"
                                            >
                                                {user.displayName || "—"}
                                            </Link>
                                        </TableCell>
                                        <TableCell>
                                            <Link
                                                to="/admin/users/$userId"
                                                params={{ userId: String(user.id) }}
                                                className="hover:text-brand-600 hover:underline"
                                            >
                                                {user.email}
                                            </Link>
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant={user.userType === "Internal" ? "success" : "neutral"}>{user.userType}</Badge>
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex flex-wrap gap-1">
                                                {user.roles.length === 0 ? (
                                                    <span className="text-muted-foreground">None</span>
                                                ) : (
                                                    user.roles.map((role) => (
                                                        <Badge key={role} variant="neutral">
                                                            {role}
                                                        </Badge>
                                                    ))
                                                )}
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant={user.status === "Active" ? "success" : "danger"}>{user.status}</Badge>
                                        </TableCell>
                                        <TableCell className="whitespace-nowrap text-muted-foreground">
                                            {formatDateTime(user.lastSignInAt)}
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </div>

                    <div className="flex items-center justify-between text-sm text-muted-foreground">
                        <span>
                            {data.totalCount} user{data.totalCount === 1 ? "" : "s"}
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
                            <Button
                                variant="outline"
                                size="sm"
                                disabled={page >= data.totalPages}
                                onClick={() => setPage((p) => p + 1)}
                            >
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
