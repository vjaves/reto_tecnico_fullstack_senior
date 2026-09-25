import { useEffect, useRef, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { useAuth } from '../auth/AuthContext'
import { IconClipboard, IconClock, IconCube, IconLogout, IconMenu, IconPlus, IconShield, IconUsers } from '../components/icons'
import { useToast } from '../components/Toast'
import { Logo, cx } from '../components/ui'

const AVISO_MS = 5 * 60 * 1000

/** Tiempo restante del token: se pone naranja en los últimos 5 minutos y avisa una sola vez. */
function SesionRestante({ expiraEn }: { expiraEn: number }) {
  const [ahora, setAhora] = useState(() => Date.now())
  const avisado = useRef(false)
  const { notify } = useToast()

  useEffect(() => {
    const id = window.setInterval(() => setAhora(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [])

  const restante = Math.max(expiraEn - ahora, 0)
  const porVencer = restante <= AVISO_MS

  useEffect(() => {
    if (porVencer && !avisado.current && restante > 0) {
      avisado.current = true
      notify('Tu sesión vence en menos de 5 minutos. Guarda tus cambios.', 'info')
    }
  }, [porVencer, restante, notify])

  const min = Math.floor(restante / 60000)
  const seg = Math.floor((restante % 60000) / 1000)

  return (
    <span
      title="Tiempo restante de la sesión"
      className={cx(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold tabular-nums',
        porVencer ? 'bg-accent-600 text-white' : 'bg-white/10 text-brand-200',
      )}
    >
      <IconClock className="size-3" />
      {String(min).padStart(2, '0')}:{String(seg).padStart(2, '0')}
    </span>
  )
}

interface Enlace {
  to: string
  label: string
  icon: ReactNode
}

const SECCIONES: { titulo: string; soloAdmin?: boolean; enlaces: Enlace[] }[] = [
  {
    titulo: 'Operaciones',
    enlaces: [
      { to: '/pedidos', label: 'Pedidos', icon: <IconClipboard className="size-5" /> },
      { to: '/pedidos/nuevo', label: 'Nuevo pedido', icon: <IconPlus className="size-5" /> },
    ],
  },
  {
    titulo: 'Maestros',
    enlaces: [
      { to: '/clientes', label: 'Clientes', icon: <IconUsers className="size-5" /> },
      { to: '/productos', label: 'Productos', icon: <IconCube className="size-5" /> },
    ],
  },
  {
    titulo: 'Administración',
    soloAdmin: true,
    enlaces: [{ to: '/admin', label: 'Panel de administración', icon: <IconShield className="size-5" /> }],
  },
]

function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const { usuario, expiraEn, logout, esAdmin } = useAuth()
  const iniciales = (usuario?.nombre ?? '?')
    .split(' ')
    .slice(0, 2)
    .map((p) => p[0])
    .join('')

  return (
    <div className="flex h-full flex-col bg-brand-950 text-white">
      {/* Franja de marca: azul primario con el acento naranja */}
      <div className="h-1 bg-gradient-to-r from-brand-600 via-brand-500 to-accent-600" />

      <div className="flex items-center gap-3 px-5 py-5">
        <Logo className="size-9" />
        <div className="leading-tight">
          <p className="font-bold tracking-tight">Atlantic</p>
          <p className="text-xs text-brand-300">Gestión de pedidos</p>
        </div>
      </div>

      <nav aria-label="Principal" className="flex-1 space-y-6 overflow-y-auto px-3 py-2">
        {SECCIONES.filter((s) => !s.soloAdmin || esAdmin).map((s) => (
          <div key={s.titulo}>
            <p className="px-3 pb-2 text-[11px] font-semibold tracking-wider text-brand-400 uppercase">{s.titulo}</p>
            <ul className="space-y-1">
              {s.enlaces.map((e) => (
                <li key={e.to}>
                  <NavLink
                    to={e.to}
                    end
                    onClick={onNavigate}
                    className={({ isActive }) =>
                      cx(
                        'group relative flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                        isActive ? 'bg-white/10 text-white' : 'text-brand-200 hover:bg-white/5 hover:text-white',
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <span
                          className={cx('absolute top-2 bottom-2 left-0 w-1 rounded-r-full bg-accent-600 transition-opacity', isActive ? 'opacity-100' : 'opacity-0')}
                          aria-hidden="true"
                        />
                        <span className={cx(isActive ? 'text-accent-500' : 'text-brand-400 group-hover:text-brand-200')}>{e.icon}</span>
                        {e.label}
                      </>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="m-3 rounded-xl bg-white/5 p-3 ring-1 ring-white/10">
        <div className="flex items-center gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-full bg-accent-600 text-sm font-bold uppercase">{iniciales}</span>
          <div className="min-w-0 flex-1 leading-tight">
            <p className="truncate text-sm font-semibold">{usuario?.nombre}</p>
            <p className="truncate text-xs text-brand-300">{usuario?.email}</p>
          </div>
        </div>
        <div className="mt-3 flex items-center justify-between gap-2">
          <span className="rounded-full bg-brand-600 px-2 py-0.5 text-[11px] font-semibold">{usuario?.rol}</span>
          {expiraEn && <SesionRestante expiraEn={expiraEn} />}
          <button
            onClick={() => logout()}
            className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-brand-200 hover:bg-white/10 hover:text-white"
          >
            <IconLogout className="size-4" />
            Salir
          </button>
        </div>
      </div>
    </div>
  )
}

export function AppLayout() {
  const [menuAbierto, setMenuAbierto] = useState(false)
  const location = useLocation()
  // El menú móvil queda asociado a la ruta en la que se abrió: al navegar se cierra solo.
  const [rutaMenu, setRutaMenu] = useState(location.pathname)
  const menuVisible = menuAbierto && rutaMenu === location.pathname

  return (
    <div className="min-h-dvh lg:pl-64">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 lg:block">
        <Sidebar />
      </aside>

      {/* Móvil: barra superior + menú lateral deslizable */}
      <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-slate-200 bg-white/90 px-4 backdrop-blur lg:hidden">
        <button
          className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
          aria-label="Abrir menú"
          aria-expanded={menuVisible}
          onClick={() => {
            setRutaMenu(location.pathname)
            setMenuAbierto(true)
          }}
        >
          <IconMenu />
        </button>
        <Logo className="size-7" />
        <span className="font-bold text-slate-900">Atlantic</span>
      </header>

      {menuVisible && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button className="absolute inset-0 bg-brand-950/50" aria-label="Cerrar menú" onClick={() => setMenuAbierto(false)} />
          <div className="relative h-full w-72 max-w-[85vw] shadow-2xl">
            <Sidebar onNavigate={() => setMenuAbierto(false)} />
          </div>
        </div>
      )}

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-10 lg:py-10">
        <div key={location.pathname} className="animate-fade-in">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
