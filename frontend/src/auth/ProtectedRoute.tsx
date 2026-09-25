import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './AuthContext'

/** Sin sesión → al login, recordando a dónde quería ir el usuario para volver después. */
export function ProtectedRoute() {
  const { usuario } = useAuth()
  const location = useLocation()

  if (!usuario) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <Outlet />
}
