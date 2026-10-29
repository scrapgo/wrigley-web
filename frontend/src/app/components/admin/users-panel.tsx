import { useState } from "react"
import { Users, MailPlus, Clock } from "lucide-react"

import { useAccess } from "../../hooks/useAccess"
import type { IssuedInvitation } from "../../lib/admin-api"
import { InviteForm, InvitationsTable } from "./invitations-panel"
import { UsersTable } from "./users-table"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "../ui/tabs"
import { HelpButton } from "../ui/help-button"

/**
 * Users tab (users.view and/or users.invite): one screen for everyone with
 * access and everyone invited, so onboarding isn't split across two tabs.
 * Each section is its own tab so nothing is hidden below the fold.
 * Active users need users.view; inviting and the pending-invitations table
 * need users.invite or users.view, matching the API's own rules.
 */
export function UsersPanel() {
    const { can } = useAccess()
    const canViewUsers = can("users.view")
    const canInvite = can("users.invite")
    // Accept links are only returned by create and resend; remember them for "Copy link".
    const [links, setLinks] = useState<Record<number, string>>({})

    const remember = (issued: IssuedInvitation) =>
        setLinks((current) => ({ ...current, [issued.invitation.id]: issued.acceptLink }))

    const defaultTab = canViewUsers ? "users" : "invite"

    return (
        <div className="space-y-4">
            <Tabs defaultValue={defaultTab}>
                {/* Horizontally scrollable pill bar on mobile so tabs never wrap awkwardly. */}
                <TabsList className="flex-wrap overflow-x-auto pb-1 whitespace-nowrap scrollbar-hide">
                    {canViewUsers && (
                        <>
                            <TabsTrigger value="users">
                                <Users className="h-4 w-4" />
                                Everyone with access
                            </TabsTrigger>
                            <TabsTrigger value="invitations">
                                <Clock className="h-4 w-4" />
                                Pending invitations
                            </TabsTrigger>
                        </>
                    )}
                    {canInvite && (
                        <TabsTrigger value="invite">
                            <MailPlus className="h-4 w-4" />
                            Invite someone
                        </TabsTrigger>
                    )}
                </TabsList>

                {canViewUsers && (
                    <TabsContent value="users" className="space-y-3">
                        <div className="flex items-center justify-between gap-2">
                            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                                <Users className="h-4 w-4" />
                                Everyone with access
                            </h2>
                            <HelpButton
                                title="Everyone with access"
                                description="The full list of people who can sign in to ScrapGo. Click a user to change their roles, supplier links, or disable their account. Use the search and filters at the top to narrow the list."
                            />
                        </div>
                        <UsersTable />
                    </TabsContent>
                )}

                {canViewUsers && (
                    <TabsContent value="invitations" className="space-y-3">
                        <div className="flex items-center justify-between gap-2">
                            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                                <Clock className="h-4 w-4" />
                                Pending invitations
                            </h2>
                            <HelpButton
                                title="Pending invitations"
                                description="Invitations you've sent that haven't been accepted yet. You can resend the link or revoke an invitation if it was sent by mistake."
                            />
                        </div>
                        <InvitationsTable links={links} canInvite={canInvite} onIssued={remember} />
                    </TabsContent>
                )}

                {canInvite && (
                    <TabsContent value="invite" className="space-y-3">
                        <div className="flex items-center justify-between gap-2">
                            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                                <MailPlus className="h-4 w-4" />
                                Invite someone
                            </h2>
                            <HelpButton
                                title="Invite someone"
                                description="Send an invitation to an external user (buyer, seller, or transporter). Choose their role and which suppliers they should see. They'll get a link to accept and set their password."
                            />
                        </div>
                        <InviteForm onIssued={remember} />
                    </TabsContent>
                )}
            </Tabs>
        </div>
    )
}
