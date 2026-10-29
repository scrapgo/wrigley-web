import { useState } from "react"
import { ChevronDown, Loader2, Lock, Target, ToggleRight, X } from "lucide-react"

import { Badge } from "../ui/badge"
import { Button } from "../ui/button"
import { Card, CardContent } from "../ui/card"
import { Checkbox } from "../ui/checkbox"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "../ui/dialog"
import { EmptyState } from "../ui/empty-state"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { useToast } from "../ui/toast"
import { useAccess } from "../../hooks/useAccess"
import { useFeatures, useRoles, useSetFeatureState, useSetFeatureTargets, useUserDetail, useUsers } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { HelpButton } from "../ui/help-button"
import { cn } from "../../lib/utils"
import type { Feature, FeatureState } from "../../lib/admin-api"

const STATES: FeatureState[] = ["Off", "On", "Targeted"]

/**
 * Features tab (features.view): each toggleable feature with an Off / On /
 * Targeted control. Locked features can never change, so they're tucked
 * behind a disclosure instead of cluttering every visit. Read-only without
 * features.manage.
 */
export function FeaturesPanel() {
    const { can } = useAccess()
    const canManage = can("features.manage")
    const { data, isLoading, isError, error } = useFeatures()
    const [targeting, setTargeting] = useState<Feature | null>(null)
    const [showLocked, setShowLocked] = useState(false)

    if (isLoading) {
        return <Skeleton className="h-64 w-full" />
    }

    if (isError || !data) {
        return <EmptyState icon={ToggleRight} title="Could not load features" description={adminErrorMessage(error)} />
    }

    const toggleable = data.filter((feature) => feature.isToggleable)
    const locked = data.filter((feature) => !feature.isToggleable)

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between gap-2">
                <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                    <ToggleRight className="h-4 w-4" />
                    Features
                </h2>
                <HelpButton
                    title="Features"
                    description="Feature flags turn parts of ScrapGo on or off. Off hides the feature for everyone; On shows it for everyone; Targeted shows it only for the users and roles you choose. Locked features are always on and can't be changed."
                />
            </div>
            {!canManage && <p className="text-sm text-muted-foreground">Read-only: changing features needs the features.manage permission.</p>}
            {toggleable.length === 0 ? (
                <EmptyState icon={ToggleRight} title="Nothing to toggle" description="Every feature here is always on." />
            ) : (
                toggleable.map((feature) => (
                    <FeatureRow key={feature.id} feature={feature} canManage={canManage} onTarget={() => setTargeting(feature)} />
                ))
            )}

            {locked.length > 0 && (
                <div className="rounded-xl border border-border bg-card">
                    <button
                        type="button"
                        onClick={() => setShowLocked((current) => !current)}
                        aria-expanded={showLocked}
                        className="flex min-h-11 w-full items-center justify-between gap-2 px-4 py-3 text-left text-sm font-medium text-muted-foreground hover:text-ink-900"
                    >
                        <span className="flex items-center gap-2">
                            <Lock className="h-4 w-4" />
                            {locked.length} system feature{locked.length === 1 ? "" : "s"} (always on)
                        </span>
                        <ChevronDown className={cn("h-4 w-4 transition-transform", showLocked && "rotate-180")} />
                    </button>
                    {showLocked && (
                        <ul className="divide-y divide-border border-t border-border">
                            {locked.map((feature) => (
                                <li key={feature.id} className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                                    <span>
                                        <span className="font-medium text-ink-900">{feature.name}</span>
                                        {feature.description && <span className="ml-2 text-muted-foreground">{feature.description}</span>}
                                    </span>
                                    <Badge variant="neutral">Always on</Badge>
                                </li>
                            ))}
                        </ul>
                    )}
                </div>
            )}

            {targeting && (
                <TargetsDialog
                    key={targeting.id}
                    feature={data.find((f) => f.id === targeting.id) ?? targeting}
                    onClose={() => setTargeting(null)}
                />
            )}
        </div>
    )
}

function FeatureRow({ feature, canManage, onTarget }: { feature: Feature; canManage: boolean; onTarget: () => void }) {
    const { toast } = useToast()
    const setState = useSetFeatureState()
    const targetCount = feature.targets.userIds.length + feature.targets.roleIds.length

    const change = async (state: FeatureState) => {
        try {
            if (state !== feature.state) {
                await setState.mutateAsync({ id: feature.id, state })
            }
            if (state === "Targeted") {
                onTarget()
            }
        } catch (err) {
            toast({ title: `Could not change ${feature.name}`, description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Card>
            <CardContent className="flex flex-col gap-4 py-5 lg:flex-row lg:items-center lg:justify-between">
                <div className="min-w-0 space-y-1">
                    <p className="flex items-center gap-2 font-semibold text-ink-900">
                        {feature.name}
                        <span className="text-xs font-normal text-muted-foreground">{feature.key}</span>
                    </p>
                    {feature.description && <p className="text-sm text-muted-foreground">{feature.description}</p>}
                    <div className="flex flex-wrap gap-1">
                        {feature.permissions.map((key) => (
                            <Badge key={key} variant="neutral">
                                {key}
                            </Badge>
                        ))}
                    </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    <div role="radiogroup" aria-label={`${feature.name} state`} className="inline-flex rounded-lg border border-border p-1">
                        {STATES.map((state) => (
                            <button
                                key={state}
                                type="button"
                                role="radio"
                                aria-checked={feature.state === state}
                                disabled={!canManage || setState.isPending}
                                onClick={() => void change(state)}
                                className={cn(
                                    "min-h-10 rounded-md px-4 text-sm font-medium transition-colors disabled:cursor-not-allowed",
                                    feature.state === state ? "bg-brand-500 text-white" : "text-ink-700 hover:bg-muted disabled:hover:bg-transparent"
                                )}
                            >
                                {state}
                            </button>
                        ))}
                    </div>
                    {feature.state === "Targeted" && (
                        <Button variant="outline" size="sm" onClick={onTarget}>
                            <Target className="h-4 w-4" />
                            {targetCount} target{targetCount === 1 ? "" : "s"}
                        </Button>
                    )}
                </div>
            </CardContent>
        </Card>
    )
}

/** Picks the users and roles a Targeted feature is on for. */
function TargetsDialog({ feature, onClose }: { feature: Feature; onClose: () => void }) {
    const { can } = useAccess()
    const canManage = can("features.manage")
    const { toast } = useToast()
    const roles = useRoles(can("roles.view"))
    const save = useSetFeatureTargets()
    const [userIds, setUserIds] = useState(() => feature.targets.userIds)
    const [roleIds, setRoleIds] = useState(() => new Set(feature.targets.roleIds))
    const [term, setTerm] = useState("")
    const search = term.trim()
    const users = useUsers({ search: search || undefined, page: 1, pageSize: 8 })

    const onSave = async () => {
        try {
            await save.mutateAsync({ id: feature.id, userIds, roleIds: [...roleIds] })
            toast({ title: `${feature.name} targets saved`, variant: "success" })
            onClose()
        } catch (err) {
            toast({ title: "Could not save targets", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Dialog open onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Who sees {feature.name}?</DialogTitle>
                    <DialogDescription>
                        While Targeted, the feature is on for these users and for everyone holding these roles. Their roles must still grant its
                        permissions.
                    </DialogDescription>
                </DialogHeader>

                <div className="space-y-5 py-2">
                    <div className="space-y-2">
                        <Label>Roles</Label>
                        {roles.isError ? (
                            <p className="text-sm text-red-600">{adminErrorMessage(roles.error)}</p>
                        ) : (
                            (roles.data ?? []).map((role) => (
                                <label key={role.id} className="flex min-h-11 cursor-pointer items-center gap-3 rounded-lg border border-border px-3">
                                    <Checkbox
                                        checked={roleIds.has(role.id)}
                                        disabled={!canManage}
                                        onCheckedChange={(checked) =>
                                            setRoleIds((current) => {
                                                const next = new Set(current)
                                                if (checked === true) next.add(role.id)
                                                else next.delete(role.id)
                                                return next
                                            })
                                        }
                                    />
                                    <span className="text-sm font-medium text-ink-900">{role.name}</span>
                                </label>
                            ))
                        )}
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="target-user-search">Users</Label>
                        {userIds.length > 0 && (
                            <div className="flex flex-wrap gap-2">
                                {userIds.map((id) => (
                                    <span key={id} className="inline-flex items-center gap-1 rounded-full bg-brand-50 py-1 pl-3 pr-1 text-sm text-brand-700">
                                        <UserLabel userId={id} />
                                        {canManage && (
                                            <button
                                                type="button"
                                                className="rounded-full p-1 hover:bg-brand-100"
                                                aria-label="Remove user"
                                                onClick={() => setUserIds((current) => current.filter((other) => other !== id))}
                                            >
                                                <X className="h-3.5 w-3.5" />
                                            </button>
                                        )}
                                    </span>
                                ))}
                            </div>
                        )}
                        {canManage && (
                            <>
                                <Input id="target-user-search" placeholder="Search users by name or email…" value={term} onChange={(e) => setTerm(e.target.value)} />
                                {search.length >= 2 && (
                                    <ul className="max-h-48 divide-y divide-border overflow-y-auto rounded-lg border border-border">
                                        {users.isError ? (
                                            <li className="px-3 py-2 text-sm text-red-600">{adminErrorMessage(users.error)}</li>
                                        ) : (
                                            (users.data?.items ?? []).map((user) => (
                                                <li key={user.id}>
                                                    <button
                                                        type="button"
                                                        disabled={userIds.includes(user.id)}
                                                        className="flex min-h-11 w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-muted disabled:opacity-50"
                                                        onClick={() => {
                                                            setUserIds((current) => [...current, user.id])
                                                            setTerm("")
                                                        }}
                                                    >
                                                        <span className="font-medium text-ink-900">{user.displayName || user.email}</span>
                                                        <span className="text-xs text-muted-foreground">{user.userType}</span>
                                                    </button>
                                                </li>
                                            ))
                                        )}
                                    </ul>
                                )}
                            </>
                        )}
                    </div>
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={onClose}>
                        {canManage ? "Cancel" : "Close"}
                    </Button>
                    {canManage && (
                        <Button disabled={save.isPending} onClick={() => void onSave()}>
                            {save.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                            Save targets
                        </Button>
                    )}
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}

function UserLabel({ userId }: { userId: number }) {
    const { data } = useUserDetail(userId)
    return <span>{data ? data.displayName || data.email : `User #${userId}`}</span>
}
