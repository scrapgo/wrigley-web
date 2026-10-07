import { useState } from "react"
import { AppWindow, Loader2 } from "lucide-react"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
import { Checkbox } from "../ui/checkbox"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
} from "../ui/dialog"
import { useToast } from "../ui/toast"
import {
    useCatalog,
    useOrganizationApplicationsAsAdmin,
    useSetApplicationAssigned,
    useSetModuleEnabled,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import type { OrganizationSummary } from "../../lib/admin-api"
import { AppointApplicationAdmin } from "./appoint-application-admin"

interface Props {
    organization: OrganizationSummary | null
    onOpenChange: (open: boolean) => void
}

/**
 * Platform administrators: which catalog applications an organization has and
 * which of their modules are enabled. Assigning or enabling grants nobody
 * anything; an application administrator grants access afterwards.
 */
export function OrganizationApplicationsDialog({ organization, onOpenChange }: Props) {
    const organizationId = organization?.id ?? 0
    const catalog = useCatalog()
    const assigned = useOrganizationApplicationsAsAdmin(organizationId)
    const setAssigned = useSetApplicationAssigned()
    const setModuleEnabled = useSetModuleEnabled()
    const { toast } = useToast()
    // Removing revokes every grant for the application here, so it's confirmed inline.
    const [confirmRemoveId, setConfirmRemoveId] = useState<number | null>(null)

    const run = async (action: () => Promise<unknown>, failure: string) => {
        try {
            await action()
        } catch (err) {
            toast({ title: failure, description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    const busy = setAssigned.isPending || setModuleEnabled.isPending
    const applications = (catalog.data ?? []).filter(
        (a) => a.status === "Active" || assigned.data?.some((x) => x.applicationId === a.id)
    )

    return (
        <Dialog open={organization !== null} onOpenChange={onOpenChange}>
            <DialogContent className="max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Applications · {organization?.name}</DialogTitle>
                    <DialogDescription>
                        Assign applications and enable licensed modules. This grants nobody
                        access: an application administrator does that.
                    </DialogDescription>
                </DialogHeader>

                {catalog.isLoading || assigned.isLoading ? (
                    <div className="space-y-2 py-2">
                        {[0, 1].map((i) => (
                            <Skeleton key={i} className="h-16 w-full" />
                        ))}
                    </div>
                ) : catalog.isError || assigned.isError ? (
                    <EmptyState
                        icon={AppWindow}
                        title="Could not load applications"
                        description={adminErrorMessage(catalog.error ?? assigned.error)}
                    />
                ) : applications.length === 0 ? (
                    <EmptyState
                        icon={AppWindow}
                        title="The catalog is empty"
                        description="No applications are defined yet. Applications and their modules are added to the catalog in code, then appear here."
                    />
                ) : (
                    <ul className="space-y-3 py-2">
                        {applications.map((application) => {
                            const entitlement = assigned.data?.find((x) => x.applicationId === application.id)
                            const isAssigned = entitlement !== undefined

                            return (
                                <li key={application.id} className="rounded-lg border border-border p-4">
                                    <div className="flex items-start justify-between gap-3">
                                        <label className="flex min-h-11 flex-1 cursor-pointer items-center gap-3">
                                            <Checkbox
                                                checked={isAssigned}
                                                disabled={busy}
                                                onCheckedChange={(checked) => {
                                                    if (checked) {
                                                        void run(
                                                            () => setAssigned.mutateAsync({ organizationId, applicationId: application.id, assigned: true }),
                                                            "Could not assign the application"
                                                        )
                                                    } else {
                                                        setConfirmRemoveId(application.id)
                                                    }
                                                }}
                                            />
                                            <span>
                                                <span className="block font-semibold text-ink-900">{application.name}</span>
                                                <span className="block font-mono text-xs text-muted-foreground">{application.key}</span>
                                            </span>
                                        </label>
                                        {application.status !== "Active" && <Badge variant="warning">Retired</Badge>}
                                        {isAssigned && application.status === "Active" && <Badge variant="success">Assigned</Badge>}
                                    </div>

                                    {confirmRemoveId === application.id && (
                                        <div className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                                            Removing {application.name} revokes every grant for it in this
                                            organization. Assigning it again restores no access.
                                            <div className="mt-3 flex gap-2">
                                                <Button
                                                    size="sm"
                                                    variant="outline"
                                                    onClick={() => setConfirmRemoveId(null)}
                                                >
                                                    Cancel
                                                </Button>
                                                <Button
                                                    size="sm"
                                                    disabled={busy}
                                                    onClick={() => {
                                                        setConfirmRemoveId(null)
                                                        void run(
                                                            () => setAssigned.mutateAsync({ organizationId, applicationId: application.id, assigned: false }),
                                                            "Could not remove the application"
                                                        )
                                                    }}
                                                >
                                                    Remove application
                                                </Button>
                                            </div>
                                        </div>
                                    )}

                                    {isAssigned && application.modules.length > 0 && (
                                        <div className="mt-3 space-y-1 border-t border-border pt-3">
                                            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                                                Modules
                                            </p>
                                            {application.modules.map((module) => {
                                                const enabled = entitlement.modules.some((m) => m.moduleId === module.id)
                                                return (
                                                    <label
                                                        key={module.id}
                                                        className="flex min-h-11 cursor-pointer items-center gap-3"
                                                    >
                                                        <Checkbox
                                                            checked={enabled}
                                                            disabled={busy || module.status !== "Active"}
                                                            onCheckedChange={(checked) =>
                                                                void run(
                                                                    () =>
                                                                        setModuleEnabled.mutateAsync({
                                                                            organizationId,
                                                                            applicationId: application.id,
                                                                            moduleId: module.id,
                                                                            enabled: checked === true,
                                                                        }),
                                                                    "Could not change the module"
                                                                )
                                                            }
                                                        />
                                                        <span className="text-sm text-ink-900">{module.name}</span>
                                                        {module.status !== "Active" && <Badge variant="warning">Retired</Badge>}
                                                    </label>
                                                )
                                            })}
                                        </div>
                                    )}

                                    {isAssigned && application.status === "Active" && (
                                        <AppointApplicationAdmin
                                            organizationId={organizationId}
                                            applicationId={application.id}
                                            applicationName={application.name}
                                        />
                                    )}
                                </li>
                            )
                        })}
                    </ul>
                )}

                {busy && (
                    <p className="flex items-center gap-2 text-xs text-muted-foreground">
                        <Loader2 className="h-3 w-3 animate-spin" /> Saving…
                    </p>
                )}
            </DialogContent>
        </Dialog>
    )
}
