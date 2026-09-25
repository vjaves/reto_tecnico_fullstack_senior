import { Link, Outlet } from 'react-router'
import { useAuth } from './AuthContext'

/**
 * Guarda de UI para las pantallas de administración. La autorización real la hace la API (política SoloAdmin):
 * esto sólo evita mostrar una pantalla que respondería 403 en cada petición.
 */
export function AdminRoute() {
  const { esAdmin } = useAuth()
  if (esAdmin) return <Outlet />

  return (
    <div className="py-24 text-center">
      <p className="text-sm font-semibold text-accent-600">403</p>
      <h1 className="mt-2 text-2xl font-bold text-slate-900">Sólo para administradores</h1>
      <p className="mt-2 text-sm text-slate-500">Tu rol no tiene acceso al panel de administración.</p>
      <Link to="/pedidos" className="mt-6 inline-block text-sm font-semibold text-brand-700 hover:underline">
        Ir a pedidos
      </Link>
    </div>
  )
}
