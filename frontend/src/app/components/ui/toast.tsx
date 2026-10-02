import * as React from "react"
import * as ToastPrimitive from "@radix-ui/react-toast"
import { cva, type VariantProps } from "class-variance-authority"
import { X, CheckCircle2, AlertTriangle, Info } from "lucide-react"

import { cn } from "../../lib/utils"

/* -------------------------------------------------------------------------- */
/*  Context                                                                    */
/* -------------------------------------------------------------------------- */

export type ToastVariant = "default" | "success" | "destructive"

export interface ToastOptions {
    title: string
    description?: string
    variant?: ToastVariant
    duration?: number
}

interface ToastItem extends ToastOptions {
    id: string
}

interface ToastContextValue {
    toast: (options: ToastOptions) => void
    dismiss: (id: string) => void
}

const ToastContext = React.createContext<ToastContextValue | undefined>(undefined)

export function ToastProvider({ children }: { children: React.ReactNode }) {
    const [toasts, setToasts] = React.useState<ToastItem[]>([])

    const dismiss = React.useCallback((id: string) => {
        setToasts((current) => current.filter((t) => t.id !== id))
    }, [])

    const toast = React.useCallback((options: ToastOptions) => {
        const id = Math.random().toString(36).slice(2)
        setToasts((current) => [...current, { ...options, id }])
    }, [])

    const value = React.useMemo(() => ({ toast, dismiss }), [toast, dismiss])

    return (
        <ToastContext.Provider value={value}>
            <ToastPrimitive.Provider swipeDirection="right">
                {children}
                {toasts.map((item) => (
                    <Toast
                        key={item.id}
                        variant={item.variant}
                        duration={item.duration}
                        onOpenChange={(open) => {
                            if (!open) dismiss(item.id)
                        }}
                    >
                        <ToastIcon variant={item.variant} />
                        <div className="grid gap-1">
                            <ToastTitle>{item.title}</ToastTitle>
                            {item.description && (
                                <ToastDescription>{item.description}</ToastDescription>
                            )}
                        </div>
                        <ToastClose />
                    </Toast>
                ))}
                <ToastViewport />
            </ToastPrimitive.Provider>
        </ToastContext.Provider>
    )
}

export function useToast() {
    const context = React.useContext(ToastContext)
    if (!context) {
        throw new Error("useToast must be used within a ToastProvider")
    }
    return context
}

/* -------------------------------------------------------------------------- */
/*  Primitives                                                                 */
/* -------------------------------------------------------------------------- */

const toastVariants = cva(
    "group pointer-events-auto relative flex w-full items-start gap-3 overflow-hidden rounded-xl border p-4 pr-8 shadow-elevated transition-all data-[swipe=cancel]:translate-x-0 data-[swipe=end]:translate-x-[var(--radix-toast-swipe-end-x)] data-[swipe=move]:translate-x-[var(--radix-toast-swipe-move-x)] data-[swipe=move]:transition-none data-[state=open]:animate-rise data-[state=closed]:animate-fade-in data-[swipe=end]:animate-fade-in",
    {
        variants: {
            variant: {
                default: "border-border bg-card text-card-foreground",
                success: "border-brand-200 bg-brand-50 text-brand-900",
                destructive: "border-red-200 bg-red-50 text-red-900",
            },
        },
        defaultVariants: {
            variant: "default",
        },
    }
)

const Toast = React.forwardRef<
    React.ElementRef<typeof ToastPrimitive.Root>,
    React.ComponentPropsWithoutRef<typeof ToastPrimitive.Root> &
    VariantProps<typeof toastVariants>
>(({ className, variant, ...props }, ref) => (
    <ToastPrimitive.Root
        ref={ref}
        className={cn(toastVariants({ variant }), className)}
        {...props}
    />
))
Toast.displayName = ToastPrimitive.Root.displayName

function ToastIcon({ variant }: { variant?: ToastVariant }) {
    if (variant === "success") {
        return <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-brand-600" />
    }
    if (variant === "destructive") {
        return <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-red-600" />
    }
    return <Info className="mt-0.5 h-5 w-5 shrink-0 text-steel-500" />
}

const ToastTitle = React.forwardRef<
    React.ElementRef<typeof ToastPrimitive.Title>,
    React.ComponentPropsWithoutRef<typeof ToastPrimitive.Title>
>(({ className, ...props }, ref) => (
    <ToastPrimitive.Title
        ref={ref}
        className={cn("text-sm font-semibold", className)}
        {...props}
    />
))
ToastTitle.displayName = ToastPrimitive.Title.displayName

const ToastDescription = React.forwardRef<
    React.ElementRef<typeof ToastPrimitive.Description>,
    React.ComponentPropsWithoutRef<typeof ToastPrimitive.Description>
>(({ className, ...props }, ref) => (
    <ToastPrimitive.Description
        ref={ref}
        className={cn("text-sm opacity-90", className)}
        {...props}
    />
))
ToastDescription.displayName = ToastPrimitive.Description.displayName

const ToastClose = React.forwardRef<
    React.ElementRef<typeof ToastPrimitive.Close>,
    React.ComponentPropsWithoutRef<typeof ToastPrimitive.Close>
>(({ className, ...props }, ref) => (
    <ToastPrimitive.Close
        ref={ref}
        className={cn(
            "absolute right-2 top-2 rounded-md p-1 text-foreground/50 opacity-0 transition-opacity hover:text-foreground focus:opacity-100 focus:outline-none focus:ring-2 group-hover:opacity-100",
            className
        )}
        toast-close=""
        {...props}
    >
        <X className="h-4 w-4" />
    </ToastPrimitive.Close>
))
ToastClose.displayName = ToastPrimitive.Close.displayName

const ToastViewport = React.forwardRef<
    React.ElementRef<typeof ToastPrimitive.Viewport>,
    React.ComponentPropsWithoutRef<typeof ToastPrimitive.Viewport>
>(({ className, ...props }, ref) => (
    <ToastPrimitive.Viewport
        ref={ref}
        className={cn(
            "fixed bottom-0 right-0 z-[100] flex max-h-screen w-full flex-col-reverse gap-2 p-4 sm:max-w-sm",
            className
        )}
        {...props}
    />
))
ToastViewport.displayName = ToastPrimitive.Viewport.displayName

export {
    Toast,
    ToastTitle,
    ToastDescription,
    ToastClose,
    ToastViewport,
}
