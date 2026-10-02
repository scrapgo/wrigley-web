import { ShieldCheck } from "lucide-react"

import { Badge } from "../ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import { usePermissions } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

const RESOURCE_LABELS: Record<string, string> = {
    User: "Users",
    Role: "Roles",
    Invoice: "Invoices",
    Report: "Reports",
    Admin: "Administration",
}

const ACTION_LABELS: Record<string, string> = {
    Read: "View",
    Create: "Create",
    Update: "Edit",
    Delete: "Delete",
    Assign: "Assign",
    Approve: "Approve",
    Export: "Export",
    Access: "Access",
}

function groupPermissions(names: string[]) {
    const groups = new Map<string, string[]>()
    for (const name of names) {
        const [resource] = name.split(".")
        const list = groups.get(resource) ?? []
        list.push(name)
        groups.set(resource, list)
    }
    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
}

export function PermissionsPanel() {
    const { data, isLoading, isError, error } = usePermissions()

    if (isLoading) {
        return (
            <div className="grid gap-4 sm:grid-cols-2">
                {[0, 1, 2, 3].map((i) => (
                    <Skeleton key={i} className="h-40 w-full" />
                ))}
            </div>
        )
    }

    if (isError) {
        return (
            <EmptyState
                icon={ShieldCheck}
                title="Could not load permissions"
                description={adminErrorMessage(error)}
            />
        )
    }

    const groups = groupPermissions((data ?? []).map((p) => p.name))

    return (
        <div className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Permission catalog</h2>
                <p className="text-sm text-muted-foreground">
                    The fixed set of permissions roles can be composed from. Read-only.
                </p>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
                {groups.map(([resource, names]) => (
                    <Card key={resource}>
                        <CardHeader className="pb-3">
                            <CardTitle className="text-base">
                                {RESOURCE_LABELS[resource] ?? resource}
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="flex flex-wrap gap-2">
                            {names.map((name) => {
                                const action = name.split(".")[1]
                                return (
                                    <Badge key={name} variant="outline" title={name}>
                                        {ACTION_LABELS[action] ?? action}
                                    </Badge>
                                )
                            })}
                        </CardContent>
                    </Card>
                ))}
            </div>
        </div>
    )
}
