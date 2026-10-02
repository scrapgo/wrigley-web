import { useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Building2, Plus } from "lucide-react"

import { Button } from "../ui/button"
import { Badge } from "../ui/badge"
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
import { useCreateOrganization, useOrganizations } from "../../hooks/useAdminQueries"
import { adminErrorMessage } from "../../lib/admin-errors"

const schema = z.object({
    name: z
        .string()
        .trim()
        .min(1, "Name is required.")
        .regex(/[a-zA-Z0-9]/, "Name must contain at least one letter or digit."),
})

type FormValues = z.infer<typeof schema>

export function OrganizationsPanel() {
    const { data, isLoading, isError, error } = useOrganizations()
    const createOrganization = useCreateOrganization()
    const { toast } = useToast()
    const [open, setOpen] = useState(false)

    const {
        register,
        handleSubmit,
        reset,
        formState: { errors },
    } = useForm<FormValues>({
        resolver: zodResolver(schema),
        defaultValues: { name: "" },
    })

    const onSubmit = handleSubmit(async (values) => {
        try {
            await createOrganization.mutateAsync(values.name)
            toast({
                title: "Organization created",
                description: `“${values.name}” is ready.`,
                variant: "success",
            })
            reset()
            setOpen(false)
        } catch (err) {
            toast({
                title: "Could not create organization",
                description: adminErrorMessage(err),
                variant: "destructive",
            })
        }
    })

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-lg font-semibold text-ink-900">Organizations</h2>
                    <p className="text-sm text-muted-foreground">
                        Organizations you belong to. Creating one makes you its administrator.
                    </p>
                </div>
                <Button onClick={() => setOpen(true)}>
                    <Plus className="h-4 w-4" />
                    New organization
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
                    icon={Building2}
                    title="Could not load organizations"
                    description={adminErrorMessage(error)}
                />
            ) : !data || data.length === 0 ? (
                <EmptyState
                    icon={Building2}
                    title="No organizations yet"
                    description="Create your first organization to start managing roles and members."
                    action={
                        <Button onClick={() => setOpen(true)}>
                            <Plus className="h-4 w-4" />
                            New organization
                        </Button>
                    }
                />
            ) : (
                <div className="rounded-xl border border-border bg-card">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Name</TableHead>
                                <TableHead>Slug</TableHead>
                                <TableHead>Status</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {data.map((org) => (
                                <TableRow key={org.id}>
                                    <TableCell className="font-medium text-ink-900">
                                        {org.name}
                                    </TableCell>
                                    <TableCell className="font-mono text-xs text-muted-foreground">
                                        {org.slug}
                                    </TableCell>
                                    <TableCell>
                                        <Badge
                                            variant={
                                                org.status === "Active" ? "success" : "neutral"
                                            }
                                        >
                                            {org.status}
                                        </Badge>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </div>
            )}

            <Dialog open={open} onOpenChange={setOpen}>
                <DialogContent>
                    <form onSubmit={onSubmit}>
                        <DialogHeader>
                            <DialogTitle>New organization</DialogTitle>
                            <DialogDescription>
                                The name is turned into a unique slug. You become its
                                OrganizationAdministrator.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-2 py-4">
                            <Label htmlFor="org-name">Name</Label>
                            <Input
                                id="org-name"
                                placeholder="Midwest Metals"
                                autoComplete="off"
                                {...register("name")}
                            />
                            {errors.name && (
                                <p className="text-sm text-red-600">{errors.name.message}</p>
                            )}
                        </div>
                        <DialogFooter>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => setOpen(false)}
                            >
                                Cancel
                            </Button>
                            <Button type="submit" disabled={createOrganization.isPending}>
                                {createOrganization.isPending ? "Creating…" : "Create"}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>
        </div>
    )
}
