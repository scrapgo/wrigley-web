import { useEffect, useState, type FormEvent } from 'react'
import { Link } from '@tanstack/react-router'
import { ChevronLeft, ChevronRight, Search, Users } from 'lucide-react'

import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Skeleton } from '../ui/skeleton'
import { EmptyState } from '../ui/empty-state'
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '../ui/table'
import { useSuppliers } from '../../hooks/useSupplierQueries'
import { adminErrorMessage } from '../../lib/admin-errors'
import { FreshnessBadge } from './freshness-badge'

const PAGE_SIZE = 25

export function SuppliersPanel({ initialSearch = '' }: { initialSearch?: string }) {
    const [searchInput, setSearchInput] = useState(initialSearch)
    const [search, setSearch] = useState(initialSearch)
    const [skip, setSkip] = useState(0)

    const { data, isLoading, isError, error, isFetching } = useSuppliers(search, skip, PAGE_SIZE)

    // The global topbar search navigates here with a `search` param. If we're
    // already on this page the component stays mounted, so sync the form state.
    useEffect(() => {
        setSearchInput(initialSearch)
        setSearch(initialSearch)
        setSkip(0)
    }, [initialSearch])

    const onSubmit = (e: FormEvent) => {
        e.preventDefault()
        setSkip(0)
        setSearch(searchInput.trim())
    }

    const total = data?.totalRecords ?? 0
    const from = total === 0 ? 0 : skip + 1
    const to = Math.min(skip + PAGE_SIZE, total)
    const hasPrev = skip > 0
    const hasNext = skip + PAGE_SIZE < total

    return (
        <div className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Suppliers</h2>
                <p className="text-sm text-muted-foreground">
                    Search the supplier directory and open a supplier for full details.
                </p>
            </div>

            {/* Search form */}
            <form onSubmit={onSubmit} className="flex gap-2">
                <div className="relative flex-1">
                    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                        value={searchInput}
                        onChange={(e) => setSearchInput(e.target.value)}
                        placeholder="Search suppliers by name..."
                        className="pl-9"
                    />
                </div>
                <Button type="submit" variant="outline">
                    <Search className="h-4 w-4" />
                    Search
                </Button>
            </form>

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2, 3, 4].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState
                    icon={Users}
                    title="Could not load suppliers"
                    description={adminErrorMessage(error)}
                />
            ) : !data || data.items.length === 0 ? (
                <EmptyState
                    icon={Users}
                    title="No suppliers found"
                    description={
                        search
                            ? 'No suppliers match that name. Try a different search.'
                            : 'There are no suppliers to show yet.'
                    }
                />
            ) : (
                <>
                    <div className="flex items-center justify-between gap-3">
                        <p className="text-sm text-muted-foreground">
                            Showing <span className="font-medium text-ink-800">{from}</span>–
                            <span className="font-medium text-ink-800">{to}</span> of{' '}
                            <span className="font-medium text-ink-800">{total}</span>
                        </p>
                        <FreshnessBadge freshness={data.freshness} />
                    </div>

                    <div className="rounded-xl border border-border bg-card">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead className="w-32">Record ID</TableHead>
                                    <TableHead>Supplier</TableHead>
                                    <TableHead className="text-right">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {data.items.map((supplier) => (
                                    <TableRow key={supplier.recordId}>
                                        <TableCell className="font-mono text-xs text-muted-foreground">
                                            {supplier.recordId}
                                        </TableCell>
                                        <TableCell className="font-medium text-ink-900">
                                            {supplier.account || '—'}
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex justify-end">
                                                <Button variant="outline" size="sm" asChild>
                                                    <Link
                                                        to="/suppliers/$recordId"
                                                        params={{ recordId: supplier.recordId.toString() }}
                                                    >
                                                        View
                                                    </Link>
                                                </Button>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </div>

                    <div className="flex items-center justify-between">
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setSkip((s) => Math.max(0, s - PAGE_SIZE))}
                            disabled={!hasPrev || isFetching}
                        >
                            <ChevronLeft className="h-4 w-4" />
                            Previous
                        </Button>
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setSkip((s) => s + PAGE_SIZE)}
                            disabled={!hasNext || isFetching}
                        >
                            Next
                            <ChevronRight className="h-4 w-4" />
                        </Button>
                    </div>
                </>
            )}
        </div>
    )
}
