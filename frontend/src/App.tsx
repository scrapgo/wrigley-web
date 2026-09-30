import { RouterProvider } from '@tanstack/react-router'
import { router } from './app/router'
import { AuthProvider } from './app/hooks/useAuth'

function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}

export default App
