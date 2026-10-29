import { HelpCircle } from "lucide-react"

import { Button } from "./button"
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "./dialog"

/** A "help" button that opens a dialog explaining a feature. */
export function HelpButton({ title, description }: { title: string; description: string }) {
    return (
        <Dialog>
            <Button variant="ghost" size="icon" className="text-muted-foreground hover:text-ink-600" aria-label={`Help: ${title}`}>
                <HelpCircle className="h-4 w-4" />
            </Button>
            <DialogContent className="max-w-md">
                <DialogHeader>
                    <DialogTitle>{title}</DialogTitle>
                    <DialogDescription>{description}</DialogDescription>
                </DialogHeader>
            </DialogContent>
        </Dialog>
    )
}
