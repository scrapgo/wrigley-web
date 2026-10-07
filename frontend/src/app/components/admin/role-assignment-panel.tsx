import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { UserPlus, UserX } from "lucide-react"

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
import { useToast } from "../ui/toast"
import {
    useAssignRole,
    useOrganizationRoles,
    useRevokeRole,
    useUserDetail,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

const assignRoleSchema = z.object({
    roleId: z.string().min(1, "Role is required"),
    organizationId: z.string().optional(),
});

type AssignRoleFormValues = z.infer<typeof assignRoleSchema>;

export function RoleAssignmentPanel({ userId }: { userId: number }) {
    const { data: user, isLoading, isError, error } = useUserDetail(userId);
    const { data: orgRoles } = useOrganizationRoles(0); // We'll need to get the correct org ID
    const assignRole = useAssignRole();
    const revokeRole = useRevokeRole();
    const { toast } = useToast();
    const [assignRoleOpen, setAssignRoleOpen] = useState(false);

    const {
        register,
        handleSubmit,
        reset,
    } = useForm<AssignRoleFormValues>({
        resolver: zodResolver(assignRoleSchema),
        defaultValues: { roleId: "", organizationId: undefined },
    });

    const onSubmit = handleSubmit(async (values) => {
        try {
            await assignRole.mutateAsync({
                userId,
                roleId: parseInt(values.roleId),
                organizationId: values.organizationId ? parseInt(values.organizationId) : undefined
            });
            toast({ title: "Role assigned", variant: "success" });
            setAssignRoleOpen(false);
            reset();
        } catch (err) {
            toast({
                title: "Could not assign role",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    });

    async function handleRevokeRole(roleId: number, organizationId?: number) {
        try {
            await revokeRole.mutateAsync({ userId, roleId, organizationId });
            toast({ title: "Role revoked", variant: "success" });
        } catch (err) {
            toast({
                title: "Could not revoke role",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    }

    if (isLoading) {
        return (
            <div className="space-y-4">
                <Skeleton className="h-8 w-64" />
                <Skeleton className="h-4 w-96" />
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            </div>
        );
    }

    if (isError) {
        return (
            <EmptyState
                icon={UserPlus}
                title="Could not load user"
                description={adminErrorMessage(error)}
            />
        );
    }

    if (!user) {
        return (
            <EmptyState
                icon={UserPlus}
                title="User not found"
                description="The requested user could not be found."
            />
        );
    }

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-lg font-semibold text-ink-900">Role Assignments</h2>
                    <p className="text-sm text-muted-foreground">
                        Manage roles assigned to {user.email}.
                    </p>
                </div>
                <Button onClick={() => setAssignRoleOpen(true)}>
                    <UserPlus className="h-4 w-4" />
                    Assign role
                </Button>
            </div>

            {user.roles.length === 0 ? (
                <EmptyState
                    icon={UserPlus}
                    title="No roles assigned"
                    description="This user has no roles assigned."
                    action={
                        <Button onClick={() => setAssignRoleOpen(true)}>
                            <UserPlus className="h-4 w-4" />
                            Assign role
                        </Button>
                    }
                />
            ) : (
                <div className="rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Role</TableHead>
                                <TableHead>Scope</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {user.roles.map((role) => (
                                <TableRow key={`${role.roleId}-${role.organizationId}`}>
                                    <TableCell className="font-medium text-ink-900">
                                        {role.name}
                                    </TableCell>
                                    <TableCell>
                                        {role.organizationId === null ? (
                                            <span className="inline-flex items-center rounded-full bg-purple-100 px-2.5 py-0.5 text-xs font-medium text-purple-800">
                                                Platform
                                            </span>
                                        ) : (
                                            <span className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-800">
                                                Organization
                                            </span>
                                        )}
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex justify-end gap-1">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleRevokeRole(role.roleId, role.organizationId || undefined)}
                                                disabled={revokeRole.isPending}
                                            >
                                                <UserX className="h-4 w-4" />
                                                Revoke
                                            </Button>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            {/* Assign role dialog */}
            <Dialog open={assignRoleOpen} onOpenChange={setAssignRoleOpen}>
                <DialogContent>
                    <form onSubmit={onSubmit}>
                        <DialogHeader>
                            <DialogTitle>Assign role</DialogTitle>
                            <DialogDescription>
                                Assign a role to this user.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-4 py-4">
                            <div className="grid gap-2">
                                <Label htmlFor="role-id">Role</Label>
                                <select
                                    id="role-id"
                                    className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                                    {...register("roleId")}
                                >
                                    <option value="">Select a role</option>
                                    {orgRoles?.map((role) => (
                                        <option key={role.id} value={role.id}>
                                            {role.name}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div className="grid gap-2">
                                <Label htmlFor="organization-id">Organization ID (optional)</Label>
                                <Input
                                    id="organization-id"
                                    type="number"
                                    placeholder="Leave blank for platform scope"
                                    {...register("organizationId")}
                                />
                            </div>
                        </div>
                        <DialogFooter>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => setAssignRoleOpen(false)}
                            >
                                Cancel
                            </Button>
                            <Button type="submit" disabled={assignRole.isPending}>
                                {assignRole.isPending ? "Assigning…" : "Assign role"}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>
        </div>
    );
}