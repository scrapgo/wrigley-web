import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Plus, Search, User, UserMinus, UserPlus } from "lucide-react"

import { Button } from "../ui/button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "../ui/table"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../ui/dialog"
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from "../ui/alert-dialog"
import { useToast } from "../ui/toast"
import {
    useAddOrganizationMember,
    useOrganizationMembers,
    useRemoveOrganizationMember,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

const addUserSchema = z.object({
    userId: z.string().min(1, "User ID is required"),
});

type AddUserFormValues = z.infer<typeof addUserSchema>;

export function OrganizationMembersPanel({ organizationId }: { organizationId: number }) {
    const { data, isLoading, isError, error } = useOrganizationMembers(organizationId);
    const addMember = useAddOrganizationMember();
    const removeMember = useRemoveOrganizationMember();
    const { toast } = useToast();
    const [addUserOpen, setAddUserOpen] = useState(false);

    const {
        register,
        handleSubmit,
        reset,
    } = useForm<AddUserFormValues>({
        resolver: zodResolver(addUserSchema),
        defaultValues: { userId: "" },
    });

    const onSubmit = handleSubmit(async (values) => {
        try {
            await addMember.mutateAsync({
                organizationId,
                userId: parseInt(values.userId)
            });
            toast({ title: "Member added", variant: "success" });
            setAddUserOpen(false);
            reset();
        } catch (err) {
            toast({
                title: "Could not add member",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    });

    async function handleRemoveMember(userId: number) {
        try {
            await removeMember.mutateAsync({ organizationId, userId });
            toast({ title: "Member removed", variant: "success" });
        } catch (err) {
            toast({
                title: "Could not remove member",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    }

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-lg font-semibold text-ink-900">Members</h2>
                    <p className="text-sm text-muted-foreground">
                        Organization members and their roles.
                    </p>
                </div>
                <Button onClick={() => setAddUserOpen(true)}>
                    <UserPlus className="h-4 w-4" />
                    Add member
                </Button>
            </div>

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState
                    icon={User}
                    title="Could not load members"
                    description={adminErrorMessage(error)}
                />
            ) : !data || data.items.length === 0 ? (
                <EmptyState
                    icon={User}
                    title="No members yet"
                    description="Add members to this organization."
                    action={
                        <Button onClick={() => setAddUserOpen(true)}>
                            <UserPlus className="h-4 w-4" />
                            Add member
                        </Button>
                    }
                />
            ) : (
                <div className="rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Email</TableHead>
                                <TableHead>Joined</TableHead>
                                <TableHead>Roles</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {data.items.map((member) => (
                                <TableRow key={member.userId}>
                                    <TableCell className="font-medium text-ink-900">
                                        {member.email}
                                    </TableCell>
                                    <TableCell className="text-muted-foreground">
                                        {new Date(member.joinedAt).toLocaleDateString()}
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex flex-wrap gap-1">
                                            {member.roles.map((role) => (
                                                <span
                                                    key={role.roleId}
                                                    className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-800"
                                                >
                                                    {role.name}
                                                </span>
                                            ))}
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex justify-end gap-1">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleRemoveMember(member.userId)}
                                                disabled={removeMember.isPending}
                                            >
                                                <UserMinus className="h-4 w-4" />
                                                Remove
                                            </Button>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            {/* Add member dialog */}
            <Dialog open={addUserOpen} onOpenChange={setAddUserOpen}>
                <DialogContent>
                    <form onSubmit={onSubmit}>
                        <DialogHeader>
                            <DialogTitle>Add member</DialogTitle>
                            <DialogDescription>
                                Add an existing user to this organization by their user ID.
                                Note: Organization admins can only add members by user ID
                                since they cannot search users (platform admin only).
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-4 py-4">
                            <div className="grid gap-2">
                                <Label htmlFor="user-id">User ID</Label>
                                <Input
                                    id="user-id"
                                    type="number"
                                    placeholder="123"
                                    {...register("userId")}
                                />
                            </div>
                        </div>
                        <DialogFooter>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => setAddUserOpen(false)}
                            >
                                Cancel
                            </Button>
                            <Button type="submit" disabled={addMember.isPending}>
                                {addMember.isPending ? "Adding…" : "Add member"}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>
        </div>
    );
}