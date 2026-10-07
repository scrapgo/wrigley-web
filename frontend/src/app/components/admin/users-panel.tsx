import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Search, UserCog, UserX, Users } from "lucide-react"
import { Link } from "@tanstack/react-router"

import { Button } from "../ui/button"
import { Input } from "../ui/input"
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
import { useToast } from "../ui/toast"
import {
    useDisableUser,
    useEnableUser,
    useUsers,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

const searchSchema = z.object({
    search: z.string().optional(),
    status: z.enum(["active", "disabled"]).optional(),
})

type SearchFormValues = z.infer<typeof searchSchema>

export function UsersPanel() {
    const [searchParams, setSearchParams] = useState<{ search?: string; status?: string }>({});
    const { data, isLoading, isError, error } = useUsers(
        searchParams.search,
        searchParams.status
    );

    const disableUser = useDisableUser();
    const enableUser = useEnableUser();
    const { toast } = useToast();

    const {
        register,
        handleSubmit,
    } = useForm<SearchFormValues>({
        resolver: zodResolver(searchSchema),
        defaultValues: { search: "", status: undefined },
    });

    const onSubmit = handleSubmit((values) => {
        setSearchParams(values);
    });

    async function handleDisableUser(id: number) {
        try {
            await disableUser.mutateAsync(id);
            toast({ title: "User disabled", variant: "success" });
        } catch (err) {
            toast({
                title: "Could not disable user",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    }

    async function handleEnableUser(id: number) {
        try {
            await enableUser.mutateAsync(id);
            toast({ title: "User enabled", variant: "success" });
        } catch (err) {
            toast({
                title: "Could not enable user",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    }

    return (
        <div className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Users</h2>
                <p className="text-sm text-muted-foreground">
                    Platform-wide user directory. Manage user accounts and permissions.
                </p>
            </div>

            {/* Search form */}
            <form onSubmit={onSubmit} className="flex gap-2">
                <div className="flex-1">
                    <Input
                        placeholder="Search by email..."
                        {...register("search")}
                    />
                </div>
                <Button type="submit" variant="outline">
                    <Search className="h-4 w-4" />
                    Search
                </Button>
            </form>

            {isLoading ? (
                <div className="space-y-2 rounded-xl border border-border bg-card p-4">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-12 w-full" />
                    ))}
                </div>
            ) : isError ? (
                <EmptyState
                    icon={Users}
                    title="Could not load users"
                    description={adminErrorMessage(error)}
                />
            ) : !data || data.items.length === 0 ? (
                <EmptyState
                    icon={Users}
                    title="No users found"
                    description="Try adjusting your search criteria."
                />
            ) : (
                <div className="rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Email</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead>Created</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {data.items.map((user) => (
                                <TableRow key={user.id}>
                                    <TableCell className="font-medium text-ink-900">
                                        {user.email}
                                    </TableCell>
                                    <TableCell>
                                        <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${user.status === "Active"
                                                ? "bg-green-100 text-green-800"
                                                : "bg-red-100 text-red-800"
                                            }`}>
                                            {user.status}
                                        </span>
                                    </TableCell>
                                    <TableCell className="text-muted-foreground">
                                        {new Date(user.createdAt).toLocaleDateString()}
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex justify-end gap-1">
                                            <Button variant="outline" size="sm" asChild>
                                                <Link to="/admin/users/$userId" params={{ userId: user.id.toString() }}>
                                                    Manage
                                                </Link>
                                            </Button>
                                            {user.status === "Active" ? (
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => handleDisableUser(user.id)}
                                                    disabled={disableUser.isPending}
                                                >
                                                    <UserX className="h-4 w-4" />
                                                    Disable
                                                </Button>
                                            ) : (
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => handleEnableUser(user.id)}
                                                    disabled={enableUser.isPending}
                                                >
                                                    <UserCog className="h-4 w-4" />
                                                    Enable
                                                </Button>
                                            )}
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}
        </div>
    );
}