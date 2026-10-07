import { AppWindow } from "lucide-react"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import { useToast } from "../ui/toast"
import { useCatalog, useSetCatalogStatus } from "../../hooks/useAdminQueries"
import { useAdminAccess } from "../../hooks/useAdminAccess"
import { adminErrorMessage } from "../../lib/admin-errors"
import type { CatalogStatus } from "../../lib/admin-api"

/**
 * The application catalog: applications → modules → permissions. Defined in
 * code and seeded by migration; platform admins with Catalog.Manage can only
 * retire or reactivate entries. Assigning to organizations is done per
 * organization (Organizations tab → Applications).
 */
export function ApplicationsPanel() {
    const { data, isLoading, isError, error } = useCatalog()
    const setStatus = useSetCatalogStatus()
    const { can } = useAdminAccess()
    const canManage = can("Catalog.Manage", null)
    const { toast } = useToast()

    const change = async (applicationId: number, moduleId: number | undefined, status: CatalogStatus) => {
        try {
            await setStatus.mutateAsync({ applicationId, moduleId, status })
        } catch (err) {
            toast({ title: "Could not change the status", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    const toggle = (status: CatalogStatus): CatalogStatus => (status === "Active" ? "Retired" : "Active")

    return (
        <div className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Applications</h2>
                <p className="text-sm text-muted-foreground">
                    The catalog of applications and their modules. Assign them to an organization
                    from the Organizations tab.
                </p>
            </div>

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1].map((i) => (
                        <Skeleton key={i} className="h-16 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState icon={AppWindow} title="Could not load the catalog" description={adminErrorMessage(error)} />
            ) : !data || data.length === 0 ? (
                <EmptyState
                    icon={AppWindow}
                    title="No applications yet"
                    description="The catalog is empty. Applications and their modules are defined in code (ApplicationCatalog) and added by a migration; they then appear here."
                />
            ) : (
                <div className="space-y-3">
                    {data.map((application) => (
                        <div key={application.id} className="rounded-xl border border-border bg-card p-4">
                            <div className="flex flex-wrap items-center justify-between gap-3">
                                <div>
                                    <p className="font-semibold text-ink-900">{application.name}</p>
                                    <p className="font-mono text-xs text-muted-foreground">{application.key}</p>
                                </div>
                                <div className="flex items-center gap-2">
                                    <Badge variant={application.status === "Active" ? "success" : "warning"}>
                                        {application.status}
                                    </Badge>
                                    {canManage && (
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            disabled={setStatus.isPending}
                                            onClick={() => void change(application.id, undefined, toggle(application.status))}
                                        >
                                            {application.status === "Active" ? "Retire" : "Reactivate"}
                                        </Button>
                                    )}
                                </div>
                            </div>

                            <ul className="mt-3 divide-y divide-border border-t border-border">
                                {application.modules.map((module) => (
                                    <li key={module.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                                        <div>
                                            <p className="text-sm font-medium text-ink-900">{module.name}</p>
                                            <p className="text-xs text-muted-foreground">
                                                {module.permissions.length > 0 ? module.permissions.join(", ") : "No permissions"}
                                            </p>
                                        </div>
                                        <div className="flex items-center gap-2">
                                            <Badge variant={module.status === "Active" ? "neutral" : "warning"}>
                                                {module.status}
                                            </Badge>
                                            {canManage && (
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    disabled={setStatus.isPending}
                                                    onClick={() => void change(application.id, module.id, toggle(module.status))}
                                                >
                                                    {module.status === "Active" ? "Retire" : "Reactivate"}
                                                </Button>
                                            )}
                                        </div>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    ))}
                </div>
            )}
        </div>
    )
}
