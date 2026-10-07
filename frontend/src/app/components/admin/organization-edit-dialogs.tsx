import { useEffect, useState } from "react"

import { Button } from "../ui/button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../ui/dialog"
import { useToast } from "../ui/toast"
import {
    useDeleteOrganization,
    useRenameOrganizationAsAdmin,
    useSetOrganizationAdministrator,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { findUserByEmail, type OrganizationSummary } from "../../lib/admin-api"

/** Same rule as the API's Organization.ToSlug. */
const toSlug = (name: string) =>
    name.trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "")

/**
 * Platform administrators: fix an organization. Rename it (the slug stays),
 * or set its administrator by user id or email, which also revokes pending
 * invitations, for an organization created with the wrong person.
 */
export function EditOrganizationDialog({
    organization,
    onOpenChange,
}: {
    organization: OrganizationSummary | null
    onOpenChange: (open: boolean) => void
}) {
    const rename = useRenameOrganizationAsAdmin()
    const setAdministrator = useSetOrganizationAdministrator()
    const { toast } = useToast()
    const [name, setName] = useState("")
    const [administrator, setAdministratorInput] = useState("")

    useEffect(() => {
        setName(organization?.name ?? "")
        setAdministratorInput("")
    }, [organization])

    if (!organization) {
        return null
    }

    const saveName = async () => {
        try {
            // The slug follows the name; the list refreshes with the new one.
            await rename.mutateAsync({ organizationId: organization.id, name })
            toast({ title: "Organization renamed", description: name, variant: "success" })
        } catch (err) {
            toast({ title: "Could not rename", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    const saveAdministrator = async () => {
        const value = administrator.trim()
        try {
            const userId = /^[1-9][0-9]*$/.test(value) ? Number(value) : (await findUserByEmail(value))?.id
            if (userId === undefined) {
                toast({
                    title: "No such user",
                    description: "They must sign in once before they can be made administrator.",
                    variant: "destructive",
                })
                return
            }
            await setAdministrator.mutateAsync({ organizationId: organization.id, userId })
            toast({
                title: "Administrator set",
                description: `${value} is now a member and administrator of ${organization.name}.`,
                variant: "success",
            })
            setAdministratorInput("")
        } catch (err) {
            toast({ title: "Could not set the administrator", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Dialog open onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Edit · {organization.name}</DialogTitle>
                    <DialogDescription>
                        Slug <span className="font-mono">{organization.slug}</span> is regenerated from
                        the new name.
                    </DialogDescription>
                </DialogHeader>

                <div className="grid gap-2 py-2">
                    <Label htmlFor="edit-org-name">Name</Label>
                    <div className="flex gap-2">
                        <Input id="edit-org-name" value={name} onChange={(e) => setName(e.target.value)} />
                        <Button
                            onClick={() => void saveName()}
                            disabled={
                                rename.isPending ||
                                (name.trim() === organization.name && toSlug(name) === organization.slug)
                            }
                        >
                            Save
                        </Button>
                    </div>
                </div>

                <div className="grid gap-2 border-t border-border py-4">
                    <Label htmlFor="edit-org-admin">Set administrator (user id or email)</Label>
                    <div className="flex gap-2">
                        <Input
                            id="edit-org-admin"
                            placeholder="35 or eric@scrapgo.com"
                            value={administrator}
                            onChange={(e) => setAdministratorInput(e.target.value)}
                        />
                        <Button
                            variant="outline"
                            onClick={() => void saveAdministrator()}
                            disabled={setAdministrator.isPending || !administrator.trim()}
                        >
                            Set
                        </Button>
                    </div>
                    <p className="text-xs text-muted-foreground">
                        Adds them as a member with OrganizationAdministrator and cancels any pending
                        invitations. Existing administrators keep their role.
                    </p>
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>
                        Done
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}

/** Platform administrators: permanently delete a deactivated organization, confirmed by typing its name. */
export function DeleteOrganizationDialog({
    organization,
    onOpenChange,
}: {
    organization: OrganizationSummary | null
    onOpenChange: (open: boolean) => void
}) {
    const remove = useDeleteOrganization()
    const { toast } = useToast()
    const [confirmation, setConfirmation] = useState("")

    useEffect(() => setConfirmation(""), [organization])

    if (!organization) {
        return null
    }

    const confirmDelete = async () => {
        try {
            await remove.mutateAsync(organization.id)
            toast({ title: "Organization deleted", description: organization.name, variant: "success" })
            onOpenChange(false)
        } catch (err) {
            toast({ title: "Could not delete", description: adminErrorMessage(err), variant: "destructive" })
        }
    }

    return (
        <Dialog open onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Delete {organization.name}?</DialogTitle>
                    <DialogDescription>
                        This permanently removes the organization with its members, roles,
                        grants, applications and invitations. It can&apos;t be undone. The audit
                        history is kept.
                    </DialogDescription>
                </DialogHeader>
                <div className="grid gap-2 py-2">
                    <Label htmlFor="delete-org-confirm">
                        Type <span className="font-semibold">{organization.name}</span> to confirm
                    </Label>
                    <Input
                        id="delete-org-confirm"
                        autoComplete="off"
                        value={confirmation}
                        onChange={(e) => setConfirmation(e.target.value)}
                    />
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>
                        Cancel
                    </Button>
                    <Button
                        className="bg-red-600 hover:bg-red-700"
                        disabled={remove.isPending || confirmation !== organization.name}
                        onClick={() => void confirmDelete()}
                    >
                        {remove.isPending ? "Deleting…" : "Delete permanently"}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}
