import { ShieldCheck, ToggleRight } from "lucide-react"

import { useAccess } from "../../hooks/useAccess"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "../ui/tabs"
import { RolesPanel } from "./roles-panel"
import { FeaturesPanel } from "./features-panel"

/**
 * Access & Features tab: roles and feature toggles are both low-frequency
 * configuration, so they share one destination instead of two top-level tabs.
 * Each section is its own sub-tab so nothing is hidden below the fold.
 * Showing the tab requires roles.view/manage or features.view/manage;
 * each sub-tab only renders what the caller is allowed to see.
 */
export function AccessPanel() {
    const { can } = useAccess()
    const canRoles = can("roles.view") || can("roles.manage")
    const canFeatures = can("features.view") || can("features.manage")

    const defaultTab = canRoles ? "roles" : "features"

    return (
        <div className="space-y-4">
            <Tabs defaultValue={defaultTab}>
                {/* All tabs visible at all times — they wrap on mobile instead of hiding off-screen. Ultra-compact. */}
                <TabsList className="flex-wrap gap-0.5">
                    {canRoles && (
                        <TabsTrigger value="roles" className="whitespace-normal text-center px-1.5 py-1 gap-1">
                            <ShieldCheck className="h-3 w-3 mb-0" />
                            <span className="block text-[0.6rem] leading-none">Roles</span>
                        </TabsTrigger>
                    )}
                    {canFeatures && (
                        <TabsTrigger value="features" className="whitespace-normal text-center px-1.5 py-1 gap-1">
                            <ToggleRight className="h-3 w-3 mb-0" />
                            <span className="block text-[0.6rem] leading-none">Features</span>
                        </TabsTrigger>
                    )}
                </TabsList>

                {canRoles && (
                    <TabsContent value="roles" className="space-y-3">
                        <RolesPanel />
                    </TabsContent>
                )}

                {canFeatures && (
                    <TabsContent value="features" className="space-y-3">
                        <FeaturesPanel />
                    </TabsContent>
                )}
            </Tabs>
        </div>
    )
}
