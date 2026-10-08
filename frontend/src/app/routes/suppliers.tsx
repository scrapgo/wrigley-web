import { createFileRoute, redirect } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { SuppliersPanel } from '../components/suppliers/suppliers-panel'

export const Route = createFileRoute('/suppliers')({
    component: SuppliersComponent,
    validateSearch: (search: Record<string, unknown>): { search?: string } => ({
        search: typeof search.search === 'string' && search.search ? search.search : undefined,
    }),
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

function SuppliersComponent() {
    const { search } = Route.useSearch()

    return (
        <AppShell
            active="suppliers"
            title="Suppliers"
            subtitle="Search the supplier directory and open a supplier for full details."
        >
            <SuppliersPanel initialSearch={search ?? ''} />
        </AppShell>
    )
}
