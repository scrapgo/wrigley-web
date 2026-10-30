import { useState } from "react"
import { ArrowRight, Clock, MailPlus, UserCheck, UserX, Users } from "lucide-react"

import { Link } from "@tanstack/react-router"

import { Badge } from "../ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { EmptyState } from "../ui/empty-state"
import { Skeleton } from "../ui/skeleton"
import { useAccess } from "../../hooks/useAccess"
import { useAudit, useInvitations, useUsers } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { formatDateTime } from "../../lib/admin-format"
import { HelpButton } from "../ui/help-button"
import type { AuditEntry, Invitation, UserSummary } from "../../lib/admin-api"

const PAGE_SIZE = 5

/** The admin home base: a quick summary of what needs attention, with one-click paths into Users, Invitations, and Audit. */
export function HomePanel() {
    const { can } = useAccess()
    const canViewUsers = can("users.view")
    const canInvite = can("users.invite")
    const canAudit = can("audit.view")

    const [selectedTab, setSelectedTab] = useState<"users" | "invitations" | "audit" | null>(null)

    const users = useUsers({ status: "Disabled", page: 1, pageSize: PAGE_SIZE })
    const invitations = useInvitations(1, PAGE_SIZE)
    const audit = useAudit({ page: 1, pageSize: PAGE_SIZE })

    const disabledUsers = users.data?.items ?? []
    const pendingInvitations = invitations.data?.items.filter((i) => i.status === "Pending") ?? []
    const recentAudit = audit.data?.items ?? []

    const isLoading = users.isLoading || invitations.isLoading || audit.isLoading
    const isError = users.isError || invitations.isError || audit.isError

    if (isLoading) {
        return (
            <div className="space-y-4">
                <Skeleton className="h-16 w-3/4" />
                <Skeleton className="h-16 w-1/2" />
                <Skeleton className="h-16 w-full" />
            </div>
        )
    }

    if (isError) {
        return (
            <EmptyState
                icon={Clock}
                title="Could not load the admin summary"
                description={adminErrorMessage(users.error ?? invitations.error ?? audit.error)}
            />
        )
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between gap-2">
                <h1 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Admin overview</h1>
                <HelpButton
                    title="Admin overview"
                    description="A quick summary of what needs attention: disabled users, pending invitations, and recent audit events. Click any card to go straight to that section."
                />
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
                {canViewUsers && (
                    <StatCard
                        title="Disabled users"
                        value={disabledUsers.length}
                        icon={UserX}
                        onClick={() => setSelectedTab("users")}
                        description="Users who can't sign in."
                    />
                )}
                {canInvite && (
                    <StatCard
                        title="Pending invitations"
                        value={pendingInvitations.length}
                        icon={MailPlus}
                        onClick={() => setSelectedTab("invitations")}
                        description="Waiting to be accepted."
                    />
                )}
                {canAudit && (
                    <StatCard
                        title="Recent events"
                        value={recentAudit.length}
                        icon={Clock}
                        onClick={() => setSelectedTab("audit")}
                        description="Latest audit log entries."
                    />
                )}
            </div>

            {selectedTab === "users" && (
                <section className="space-y-3">
                    <div className="flex items-center justify-between gap-2">
                        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                            <Users className="h-4 w-4" />
                            Disabled users
                        </h2>
                        <Link
                            to="/admin"
                            search={{ tab: "users" }}
                            className="text-xs font-medium text-brand-600 hover:underline"
                        >
                            Manage all users <ArrowRight className="h-3.5 w-3.5 ml-1" />
                        </Link>
                    </div>
                    {disabledUsers.length === 0 ? (
                        <EmptyState icon={UserCheck} title="No disabled users" description="Everyone can sign in." />
                    ) : (
                        <div className="space-y-2">
                            {disabledUsers.map((user) => (
                                <DisabledUserRow key={user.id} user={user} />
                            ))}
                        </div>
                    )}
                </section>
            )}

            {selectedTab === "invitations" && (
                <section className="space-y-3">
                    <div className="flex items-center justify-between gap-2">
                        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                            <MailPlus className="h-4 w-4" />
                            Pending invitations
                        </h2>
                        <Link
                            to="/admin"
                            search={{ tab: "users" }}
                            className="text-xs font-medium text-brand-600 hover:underline"
                        >
                            Manage invitations <ArrowRight className="h-3.5 w-3.5 ml-1" />
                        </Link>
                    </div>
                    {pendingInvitations.length === 0 ? (
                        <EmptyState icon={MailPlus} title="No pending invitations" description="All invitations have been accepted or revoked." />
                    ) : (
                        <div className="space-y-2">
                            {pendingInvitations.map((invitation) => (
                                <InvitationRow key={invitation.id} invitation={invitation} />
                            ))}
                        </div>
                    )}
                </section>
            )}

            {selectedTab === "audit" && (
                <section className="space-y-3">
                    <div className="flex items-center justify-between gap-2">
                        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                            <Clock className="h-4 w-4" />
                            Recent audit events
                        </h2>
                        <Link
                            to="/admin"
                            search={{ tab: "audit" }}
                            className="text-xs font-medium text-brand-600 hover:underline"
                        >
                            View full log <ArrowRight className="h-3.5 w-3.5 ml-1" />
                        </Link>
                    </div>
                    {recentAudit.length === 0 ? (
                        <EmptyState icon={Clock} title="No recent audit events" description="Nothing has been logged yet." />
                    ) : (
                        <div className="space-y-2">
                            {recentAudit.map((entry) => (
                                <AuditRow key={entry.id} entry={entry} />
                            ))}
                        </div>
                    )}
                </section>
            )}
        </div>
    )
}

function StatCard({
    title,
    value,
    icon: Icon,
    onClick,
    description,
}: {
    title: string
    value: number
    icon: React.ComponentType<{ className?: string }>
    onClick: () => void
    description: string
}) {
    return (
        <Card onClick={onClick} className="cursor-pointer hover:bg-muted transition-colors">
            <CardHeader className="flex flex-row items-center justify-between py-3">
                <CardTitle className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{title}</CardTitle>
                <Icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent className="pt-2">
                <div className="text-xl font-bold text-ink-900">{value}</div>
                <p className="text-[0.7rem] text-muted-foreground mt-1">{description}</p>
            </CardContent>
        </Card>
    )
}

function DisabledUserRow({ user }: { user: UserSummary }) {
    return (
        <div className="flex items-center justify-between gap-3 px-3 py-2 rounded-lg border border-border bg-card hover:bg-muted/50">
            <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-ink-900 truncate">{user.displayName || user.email}</p>
                <p className="text-xs text-muted-foreground truncate">{user.email}</p>
            </div>
            <Badge variant="danger" className="text-[0.75rem]">
                Disabled
            </Badge>
        </div>
    )
}

function InvitationRow({ invitation }: { invitation: Invitation }) {
    return (
        <div className="flex items-center justify-between gap-3 px-3 py-2 rounded-lg border border-border bg-card hover:bg-muted/50">
            <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-ink-900 truncate">{invitation.email}</p>
                <p className="text-xs text-muted-foreground truncate">
                    {invitation.roleKey} · {invitation.supplierRecordIds.map((id) => `#${id}`).join(", ") || "No suppliers"}
                </p>
            </div>
            <Badge variant="warning" className="text-[0.75rem]">
                Pending
            </Badge>
        </div>
    )
}

function AuditRow({ entry }: { entry: AuditEntry }) {
    return (
        <div className="flex items-center justify-between gap-3 px-3 py-2 rounded-lg border border-border bg-card hover:bg-muted/50">
            <div className="flex-1 min-w-0">
                <p className="text-xs font-mono text-ink-900 truncate">{entry.action}</p>
                <p className="text-xs text-muted-foreground truncate">
                    {entry.actorEmail ?? `User #${entry.actorUserId}`} · {formatDateTime(entry.eventTime)}
                </p>
            </div>
            <Badge variant={entry.action === "access.denied" ? "danger" : "success"} className="text-[0.75rem]">
                {entry.action === "access.denied" ? "Denied" : "Success"}
            </Badge>
        </div>
    )
}
