import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Building2, Pencil, Save, Users } from "lucide-react"

import { Button } from "../ui/button"
import { Input } from "../ui/input"
import { Label } from "../ui/label"
import { Skeleton } from "../ui/skeleton"
import { EmptyState } from "../ui/empty-state"
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card"
import { useToast } from "../ui/toast"
import {
    useOrganizationDetail,
    useUpdateOrganization,
} from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"
import { OrganizationMembersPanel } from "./organization-members-panel"

const renameSchema = z.object({
    name: z.string().min(1, "Name is required"),
});

type RenameFormValues = z.infer<typeof renameSchema>;

export function OrganizationDetailPanel({ organizationId }: { organizationId: number }) {
    const { data: org, isLoading, isError, error } = useOrganizationDetail(organizationId);
    const updateOrg = useUpdateOrganization();
    const { toast } = useToast();
    const [editing, setEditing] = useState(false);

    const {
        register,
        handleSubmit,
        reset,
    } = useForm<RenameFormValues>({
        resolver: zodResolver(renameSchema),
        defaultValues: { name: org?.name || "" },
    });

    const onSubmit = handleSubmit(async (values) => {
        try {
            await updateOrg.mutateAsync({ organizationId, name: values.name });
            toast({ title: "Organization renamed", variant: "success" });
            setEditing(false);
        } catch (err) {
            toast({
                title: "Could not rename organization",
                description: adminErrorMessage(err),
                variant: "destructive",
            });
        }
    });

    if (isLoading) {
        return (
            <div className="space-y-4">
                <Skeleton className="h-8 w-64" />
                <Skeleton className="h-4 w-96" />
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                    {[0, 1, 2].map((i) => (
                        <Skeleton key={i} className="h-24 w-full" />
                    ))}
                </div>
            </div>
        );
    }

    if (isError) {
        return (
            <EmptyState
                icon={Building2}
                title="Could not load organization"
                description={adminErrorMessage(error)}
            />
        );
    }

    if (!org) {
        return (
            <EmptyState
                icon={Building2}
                title="Organization not found"
                description="The requested organization could not be found."
            />
        );
    }

    return (
        <div className="space-y-6">
            <div>
                <h2 className="text-lg font-semibold text-ink-900">Organization Details</h2>
                <p className="text-sm text-muted-foreground">
                    Manage organization settings and members.
                </p>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center justify-between">
                        <span className="flex items-center gap-2">
                            <Building2 className="h-5 w-5" />
                            {org.name}
                        </span>
                        {!editing && (
                            <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                                <Pencil className="h-4 w-4" />
                                Rename
                            </Button>
                        )}
                    </CardTitle>
                </CardHeader>
                <CardContent>
                    {editing ? (
                        <form onSubmit={onSubmit} className="flex gap-2">
                            <div className="flex-1">
                                <Input
                                    defaultValue={org.name}
                                    {...register("name")}
                                />
                            </div>
                            <Button type="submit" disabled={updateOrg.isPending}>
                                {updateOrg.isPending ? (
                                    <>
                                        <Save className="h-4 w-4 animate-spin" />
                                        Saving...
                                    </>
                                ) : (
                                    <>
                                        <Save className="h-4 w-4" />
                                        Save
                                    </>
                                )}
                            </Button>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => {
                                    setEditing(false);
                                    reset({ name: org.name });
                                }}
                            >
                                Cancel
                            </Button>
                        </form>
                    ) : (
                        <div className="space-y-2">
                            <div className="text-sm">
                                <span className="font-medium">ID:</span> {org.id}
                            </div>
                            <div className="text-sm">
                                <span className="font-medium">Slug:</span> {org.slug}
                            </div>
                            <div className="text-sm">
                                <span className="font-medium">Status:</span> {org.status}
                            </div>
                            <div className="text-sm">
                                <span className="font-medium">Created:</span>{" "}
                                {new Date(org.createdAt).toLocaleDateString()}
                            </div>
                            <div className="text-sm">
                                <span className="font-medium">Active Members:</span> {org.activeMemberCount}
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>

            <OrganizationMembersPanel organizationId={organizationId} />
        </div>
    );
}