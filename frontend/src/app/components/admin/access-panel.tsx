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
                {/* Horizontally scrollable pill bar on mobile so tabs never wrap awkwardly. */}
                <TabsList className="flex-wrap overflow-x-auto pb-1 whitespace-nowrap scrollbar-hide">
                    {canRoles && (
                        <TabsTrigger value="roles">
                            <ShieldCheck className="h-4 w-4" />
                            Roles
                        </TabsTrigger>
                    )}
                    {canFeatures && (
                        <TabsTrigger value="features">
                            <ToggleRight className="h-4 w-4" />
                            Features
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
