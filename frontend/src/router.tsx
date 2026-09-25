import { Link, Navigate, createBrowserRouter } from 'react-router'
import { AdminRoute } from './auth/AdminRoute'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AdminPage } from './features/admin/AdminPage'
import { ClientesPage } from './features/clientes/ClientesPage'
import { ProductosPage } from './features/productos/ProductosPage'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { PedidoFormPage } from './pages/PedidoFormPage'
import { PedidosListPage } from './pages/PedidosListPage'

function NoEncontrada() {
  return (
    <div className="py-24 text-center">
      <p className="text-sm font-semibold text-accent-600">404</p>
      <h1 className="mt-2 text-2xl font-semibold text-slate-900">Página no encontrada</h1>
      <Link to="/pedidos" className="mt-6 inline-block text-sm font-medium text-brand-700 hover:underline">
        Ir a pedidos
      </Link>
    </div>
  )
}

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <Navigate to="/pedidos" replace /> },
          { path: 'pedidos', element: <PedidosListPage /> },
          { path: 'pedidos/nuevo', element: <PedidoFormPage key="nuevo" /> },
          { path: 'pedidos/:id/editar', element: <PedidoFormPage key="editar" /> },
          { path: 'clientes', element: <ClientesPage /> },
          { path: 'productos', element: <ProductosPage /> },
          { element: <AdminRoute />, children: [{ path: 'admin', element: <AdminPage /> }] },
          { path: '*', element: <NoEncontrada /> },
        ],
      },
    ],
  },
])
