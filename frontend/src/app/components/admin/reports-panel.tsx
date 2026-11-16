import { useState } from 'react'
import { Calendar, FileText, Receipt } from 'lucide-react'

import { Button } from '../ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { useToast } from '../ui/toast'
import { useTriggerMissingInvoicesReport, useTriggerRemittanceReport } from '../../hooks/useAdminQueries'
import { adminErrorMessage } from '../../lib/admin-errors'

/** The admin tab that triggers the retired Qhuckbase-style reports. */
export function ReportsPanel() {
    const [remitDate, setRemitDate] = useState(todayIso())
    const [missingDate, setMissingDate] = useState(todayIso())

    const missing = useTriggerMissingInvoicesReport()
    const remittance = useTriggerRemittanceReport()
    const { toast } = useToast()

    const onMissing = async () => {
        try {
            const result = await missing.mutateAsync(missingDate || undefined)
            toast({
                title: 'Missing Invoice report triggered',
                description: `Queued for ${result.reportDate}.`,
                variant: 'success',
            })
        } catch (err) {
            toast({ title: 'Could not trigger report', description: adminErrorMessage(err), variant: 'destructive' })
        }
    }

    const onRemittance = async () => {
        if (!remitDate) {
            toast({ title: 'Select a date', description: 'A remittance date is required.', variant: 'destructive' })
            return
        }
        try {
            const result = await remittance.mutateAsync(remitDate)
            toast({
                title: 'Remittance report triggered',
                description: `Queued for ${result.reportDate}.`,
                variant: 'success',
            })
        } catch (err) {
            toast({ title: 'Could not trigger report', description: adminErrorMessage(err), variant: 'destructive' })
        }
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between gap-2">
                <h1 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Reports</h1>
            </div>

            <p className="text-sm text-muted-foreground">
                Trigger the retired Qhuckbase-style reports through their n8n webhooks. The
                webhook URLs and shared token live in configuration on the API; they never
                reach the browser.
            </p>

            <div className="grid gap-6 lg:grid-cols-2">
                <ReportCard
                    icon={Receipt}
                    title="Missing Invoice Report"
                    description="Fires the report for missing invoice information."
                    date={missingDate}
                    onDateChange={setMissingDate}
                    label="Report date"
                    buttonLabel="Run Missing Invoice Report"
                    isPending={missing.isPending}
                    onClick={onMissing}
                />

                <ReportCard
                    icon={FileText}
                    title="Remittance Report"
                    description="Fires the remittance report for the selected date."
                    date={remitDate}
                    onDateChange={setRemitDate}
                    label="Remittance Date"
                    buttonLabel="Run Remittance Report"
                    isPending={remittance.isPending}
                    onClick={onRemittance}
                />
            </div>
        </div>
    )
}

function ReportCard({
    icon: Icon,
    title,
    description,
    date,
    onDateChange,
    label,
    buttonLabel,
    isPending,
    onClick,
}: {
    icon: React.ComponentType<{ className?: string }>
    title: string
    description: string
    date: string
    onDateChange: (value: string) => void
    label: string
    buttonLabel: string
    isPending: boolean
    onClick: () => void
}) {
    return (
        <Card>
            <CardHeader>
                <div className="flex items-center gap-2">
                    <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
                        <Icon className="h-4 w-4" />
                    </span>
                    <div>
                        <CardTitle className="text-base font-semibold">{title}</CardTitle>
                        <CardDescription>{description}</CardDescription>
                    </div>
                </div>
            </CardHeader>
            <CardContent className="space-y-3">
                <div className="space-y-1.5">
                    <Label htmlFor={`${label}-date`}>{label}</Label>
                    <Input
                        id={`${label}-date`}
                        type="date"
                        value={date}
                        onChange={(e) => onDateChange(e.target.value)}
                    />
                </div>
                <Button className="w-full" disabled={isPending} onClick={onClick}>
                    <Calendar className="h-4 w-4" />
                    {isPending ? 'Triggering…' : buttonLabel}
                </Button>
            </CardContent>
        </Card>
    )
}

function todayIso(): string {
    return new Date().toISOString().split('T')[0]
}