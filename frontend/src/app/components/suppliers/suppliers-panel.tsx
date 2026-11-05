import { useMemo, useState, type FormEvent } from 'react'
import { Link } from '@tanstack/react-router'
import { ChevronLeft, ChevronRight, Search, SortAsc, SortDesc, Filter, X, Eye, Edit } from 'lucide-react'

import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '../ui/table'
import { useSuppliers } from '../../hooks/useSupplierQueries'
import { useAccess } from '../../hooks/useAccess'
import { htmlToText } from '../../lib/supplier-format'

const PAGE_SIZE = 25
const FETCH_PAGE_SIZE = 1000

type SortKey = 'recordId' | 'account' | 'city' | 'state' | 'totalActivities'

interface SortState {
    key: SortKey
    direction: 'asc' | 'desc'
}

const DEFAULT_SORT: SortState = { key: 'account', direction: 'asc' }

interface FilterState {
    recordId: string
    account: string
    city: string
    state: string
}

const DEFAULT_FILTER: FilterState = { recordId: '', account: '', city: '', state: '' }

/** Radix Select can't hold an empty value, so "All states" uses this and maps to ''. */
const ALL_STATES = 'all'

/** A column filter box with a button that clears it while it has a value. */
function FilterInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
    return (
        <div className="relative">
            <Input
                value={value}
                onChange={(e) => onChange(e.target.value)}
                placeholder="Filter"
                aria-label={`Filter by ${label}`}
                className="h-7 pr-7 text-xs border-none bg-transparent focus:ring-0"
            />
            {value && <ClearButton label={label} onClick={() => onChange('')} />}
        </div>
    )
}

function ClearButton({ label, onClick }: { label: string; onClick: () => void }) {
    return (
        <button
            type="button"
            onClick={onClick}
            aria-label={`Clear ${label} filter`}
            className="absolute right-0 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-ink-900"
        >
            <X className="h-3.5 w-3.5" />
        </button>
    )
}

/** US state abbreviations for the state filter dropdown. */
const STATE_ABBREVIATIONS = [
    'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA', 'HI', 'ID', 'IL', 'IN', 'IA', 'KS',
    'KY', 'LA', 'ME', 'MD', 'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ', 'NM', 'NY',
    'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC', 'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV',
    'WI', 'WY',
]

export function SuppliersPanel({ initialSearch = '' }: { initialSearch?: string }) {
    const [searchInput, setSearchInput] = useState(initialSearch)
    const [search, setSearch] = useState(initialSearch)
    const [skip, setSkip] = useState(0)
    const [sort, setSort] = useState<SortState>(DEFAULT_SORT)
    const [filter, setFilter] = useState<FilterState>(DEFAULT_FILTER)
    const [showFilters, setShowFilters] = useState(false)

    const { data, isFetching } = useSuppliers(search, 0, FETCH_PAGE_SIZE)
    const { can } = useAccess()
    const canViewInternal = can('suppliers.view_all')

    // The global topbar search navigates here with a `search` param. If we're
    // already on this page the component stays mounted, so sync the form state
    // during render when the param changes (no effect needed).
    const [syncedSearch, setSyncedSearch] = useState(initialSearch)
    if (initialSearch !== syncedSearch) {
        setSyncedSearch(initialSearch)
        setSearchInput(initialSearch)
        setSearch(initialSearch)
        setSkip(0)
    }

    const onSubmit = (e: FormEvent) => {
        e.preventDefault()
        setSkip(0)
        setSearch(searchInput.trim())
    }

    // Any filter change starts again at page 1, so a narrower list never leaves an empty page.
    const setFilterValue = (key: keyof FilterState, value: string) => {
        setFilter((current) => ({ ...current, [key]: value }))
        setSkip(0)
    }

    const activeFilterCount = Object.values(filter).filter(Boolean).length
    const anythingToClear = activeFilterCount > 0 || searchInput !== '' || search !== ''

    const clearAll = () => {
        setFilter(DEFAULT_FILTER)
        setSearchInput('')
        setSearch('')
        setSkip(0)
    }

    const items = useMemo(() => {
        let list = data?.items ?? []

        // Apply column filters
        if (filter.recordId) {
            const term = filter.recordId.toLowerCase()
            list = list.filter((s) => String(s.recordId).toLowerCase().includes(term))
        }
        if (filter.account) {
            const term = filter.account.toLowerCase()
            list = list.filter((s) => (s.account ?? '').toLowerCase().includes(term))
        }
        if (filter.city) {
            const term = filter.city.toLowerCase()
            list = list.filter((s) => (s.city ?? '').toLowerCase().includes(term))
        }
        if (filter.state) {
            const term = filter.state.toUpperCase()
            list = list.filter((s) => (s.state ?? '').toUpperCase() === term)
        }

        // Apply sort
        return [...list].sort((a, b) => {
            const aVal = a[sort.key]
            const bVal = b[sort.key]
            if (aVal == null && bVal == null) return 0
            if (aVal == null) return 1
            if (bVal == null) return -1
            const cmp = String(aVal).localeCompare(String(bVal), undefined, { numeric: true })
            return sort.direction === 'asc' ? cmp : -cmp
        })
    }, [data, sort, filter])

    const total = items.length
    const hasPrev = skip > 0
    const hasNext = skip + PAGE_SIZE < total

    const sortIcon = (key: SortKey) => {
        if (sort.key !== key) return null
        return sort.direction === 'asc' ? <SortAsc className="h-3 w-3" /> : <SortDesc className="h-3 w-3" />
    }

    const sortableHeader = (key: SortKey, label: string, className?: string) => {
        return (
            <TableHead key={key} className={className}>
                <div
                    className="flex items-center justify-between gap-2 cursor-pointer hover:bg-muted/50 rounded px-2 py-1"
                    onClick={() =>
                        setSort((prev) =>
                            prev.key === key
                                ? { ...prev, direction: prev.direction === 'asc' ? 'desc' : 'asc' }
                                : { key, direction: 'asc' }
                        )
                    }
                >
                    <span className="text-xs font-medium text-muted-foreground">{label}</span>
                    {sortIcon(key)}
                </div>
            </TableHead>
        )
    }

    return (
        <div className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Suppliers</h2>
                <p className="text-sm text-muted-foreground">
                    Search the supplier directory and open a supplier for full details.
                </p>
            </div>

            {/* Global search form */}
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
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setShowFilters((prev) => !prev)}
                >
                    <Filter className="h-3 w-3 mr-1" />
                    {showFilters ? 'Hide Filters' : 'Filters'}
                    {activeFilterCount > 0 && ` (${activeFilterCount})`}
                </Button>
                {anythingToClear && (
                    <Button type="button" variant="ghost" size="sm" onClick={clearAll}>
                        <X className="h-3 w-3 mr-1" />
                        Clear filters
                    </Button>
                )}
            </form>

            <div className="rounded-xl border border-border bg-card overflow-x-auto">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead className="w-20 text-center">Actions</TableHead>
                            {sortableHeader('recordId', 'Record ID', 'min-w-[120px] sm:min-w-[160px]')}
                            {sortableHeader('account', 'Supplier', 'min-w-[160px] sm:min-w-[240px]')}
                            {sortableHeader('city', 'City', 'min-w-[100px] sm:min-w-[140px]')}
                            {sortableHeader('state', 'State', 'min-w-[80px] sm:min-w-[100px]')}
                            {sortableHeader('totalActivities', 'Total Activity', 'min-w-[110px] sm:min-w-[140px] text-right')}
                        </TableRow>
                        {showFilters && (
                            <TableRow>
                                <TableHead className="w-20 text-center">Actions</TableHead>
                                <TableHead className="w-32">
                                    <FilterInput label="Record ID" value={filter.recordId} onChange={(value) => setFilterValue('recordId', value)} />
                                </TableHead>
                                <TableHead className="min-w-[160px] sm:min-w-[240px]">
                                    <FilterInput label="Supplier" value={filter.account} onChange={(value) => setFilterValue('account', value)} />
                                </TableHead>
                                <TableHead className="min-w-[100px] sm:min-w-[140px]">
                                    <FilterInput label="City" value={filter.city} onChange={(value) => setFilterValue('city', value)} />
                                </TableHead>
                                <TableHead className="min-w-[80px] sm:min-w-[100px]">
                                    <div className="relative">
                                        <Select
                                            value={filter.state || ALL_STATES}
                                            onValueChange={(value) => setFilterValue('state', value === ALL_STATES ? '' : value)}
                                        >
                                            <SelectTrigger
                                                aria-label="Filter by State"
                                                className={`h-7 text-xs border-none bg-transparent focus:ring-0 ${filter.state ? 'pr-7' : ''}`}
                                            >
                                                <SelectValue placeholder="Filter" />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value={ALL_STATES}>All states</SelectItem>
                                                {STATE_ABBREVIATIONS.map((abbr) => (
                                                    <SelectItem key={abbr} value={abbr}>
                                                        {abbr}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                        {filter.state && <ClearButton label="State" onClick={() => setFilterValue('state', '')} />}
                                    </div>
                                </TableHead>
                                <TableHead className="min-w-[110px] sm:min-w-[140px] text-right">
                                    <div className="flex items-center justify-end gap-2">
                                        <span className="text-xs font-medium text-muted-foreground">Total Activity</span>
                                    </div>
                                </TableHead>
                                <TableHead className="text-right min-w-[100px]">Actions</TableHead>
                            </TableRow>
                        )}
                    </TableHeader>
                    <TableBody>
                        {items.slice(skip, skip + PAGE_SIZE).map((supplier) => (
                            <TableRow key={supplier.recordId}>
                                <TableCell className="w-20 text-center">
                                    <div className="flex justify-center gap-2">
                                        <Button variant="outline" size="sm" asChild className="p-1.5" title="View Supplier Dashboard">
                                            <Link
                                                to="/supplier/dashboard/$recordId"
                                                params={{ recordId: supplier.recordId.toString() }}
                                            >
                                                <Eye className="h-4 w-4" />
                                            </Link>
                                        </Button>
                                        {canViewInternal && (
                                            <Button variant="outline" size="sm" asChild className="p-1.5" title="View Admin Details">
                                                <Link
                                                    to="/suppliers/$recordId"
                                                    params={{ recordId: supplier.recordId.toString() }}
                                                >
                                                    <Edit className="h-4 w-4" />
                                                </Link>
                                            </Button>
                                        )}
                                    </div>
                                </TableCell>
                                <TableCell className="font-mono text-xs text-muted-foreground">
                                    {supplier.recordId}
                                </TableCell>
                                <TableCell className="font-medium text-ink-900">
                                    {htmlToText(supplier.account) || '—'}
                                </TableCell>
                                <TableCell className="text-xs text-muted-foreground">
                                    {htmlToText(supplier.city) || '—'}
                                </TableCell>
                                <TableCell className="text-xs text-muted-foreground">
                                    {htmlToText(supplier.state) || '—'}
                                </TableCell>
                                <TableCell className="text-xs text-muted-foreground text-right">
                                    {supplier.totalActivities ?? 0} loads
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
        </div>
    )
}
