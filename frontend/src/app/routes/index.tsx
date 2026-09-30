import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
    component: IndexComponent,
    beforeLoad: async () => {
        // Check if user is authenticated
        const token = localStorage.getItem('authToken')
        if (token) {
            throw redirect({
                to: '/dashboard',
            })
        } else {
            throw redirect({
                to: '/login',
            })
        }
    }
})

function IndexComponent() {
    return null
}