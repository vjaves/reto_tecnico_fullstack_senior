import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { toApiError, type ApiError } from '../api/http'
import { pedidosApi } from '../api/services'
import { useAuth } from '../auth/AuthContext'
import { IconChevron, IconPencil, IconPlus, IconSearch, IconTrash } from '../components/icons'
import { useToast } from '../components/Toast'
import { Alert, Button, Card, ConfirmDialog, EmptyState, EstadoBadge, Input, PageHeader, Paginacion, SkeletonRows, Spinner, cx } from '../components/ui'
import { useDebounce } from '../hooks/useDebounce'
import { ESTADOS_EDITABLES, type EstadoPedido, type PagedResult, type PedidoFiltro, type PedidoResumen, type PedidoResumenEstados } from '../types'
import { formatFecha, formatMoney } from '../utils/format'

type Orden = NonNullable<PedidoFiltro['ordenarPor']>

/** Los filtros viven en la URL: se pueden compartir, sobreviven a F5 y el botón Atrás los respeta. */
function useFiltroEnUrl() {
  const [params, setParams] = useSearchParams()

  const filtro: PedidoFiltro = useMemo(
    () => ({
      buscar: params.get('buscar') ?? '',
      estado: (params.get('estado') as EstadoPedido | null) ?? '',
      desde: params.get('desde') ?? '',
      hasta: params.get('hasta') ?? '',
      page: Math.max(Number(params.get('page')) || 1, 1),
      pageSize: Number(params.get('pageSize')) || 10,
      ordenarPor: (params.get('orden') as Orden | null) ?? undefined,
      descendente: params.get('asc') !== '1',
    }),
    [params],
  )

  const actualizar = useCallback(
    (cambios: Partial<PedidoFiltro>, reiniciarPagina = true) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          const mapa: Record<string, string | undefined> = {
            buscar: cambios.buscar,
            estado: cambios.estado,
            desde: cambios.desde,
            hasta: cambios.hasta,
            page: cambios.page?.toString(),
            pageSize: cambios.pageSize?.toString(),
            orden: cambios.ordenarPor,
            asc: cambios.descendente === undefined ? undefined : cambios.descendente ? '' : '1',
          }
          for (const [k, v] of Object.entries(mapa)) {
            if (v === undefined) continue
            if (v === '') next.delete(k)
            else next.set(k, v)
          }
          if (reiniciarPagina && cambios.page === undefined) next.delete('page')
          return next
        },
        { replace: true },
      )
    },
    [setParams],
  )

  const limpiar = useCallback(() => setParams({}, { replace: true }), [setParams])

  return { filtro, actualizar, limpiar }
}

const TARJETA_ESTADO: Record<string, { punto: string; activo: string }> = {
  '': { punto: 'bg-brand-600', activo: 'ring-brand-600 bg-brand-50/60' },
  Registrado: { punto: 'bg-slate-400', activo: 'ring-slate-500 bg-slate-50' },
  Confirmado: { punto: 'bg-brand-600', activo: 'ring-brand-600 bg-brand-50/60' },
  Despachado: { punto: 'bg-accent-600', activo: 'ring-accent-600 bg-accent-50/60' },
  Entregado: { punto: 'bg-emerald-500', activo: 'ring-emerald-500 bg-emerald-50/60' },
}

/** Tarjetas de conteo por estado; hacer clic en una filtra la tabla. */
function ResumenEstados({ resumen, estado, onEstado }: { resumen: PedidoResumenEstados | null; estado: string; onEstado: (e: EstadoPedido | '') => void }) {
  const items: { key: EstadoPedido | ''; label: string; valor?: number }[] = [
    { key: '', label: 'Todos', valor: resumen?.total },
    ...ESTADOS_EDITABLES.map((e) => ({ key: e, label: e, valor: resumen?.porEstado[e] })),
  ]
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5" role="group" aria-label="Filtrar por estado">
      {items.map((i) => {
        const seleccionado = estado === i.key
        return (
          <button
            key={i.key || 'todos'}
            onClick={() => onEstado(i.key)}
            aria-pressed={seleccionado}
            className={cx(
              'group rounded-xl bg-white p-4 text-left shadow-(--shadow-card) ring-1 transition-all hover:-translate-y-0.5 hover:shadow-md',
              seleccionado ? `ring-2 ${TARJETA_ESTADO[i.key].activo}` : 'ring-slate-200/80',
            )}
          >
            <span className="flex items-center gap-2 text-xs font-semibold tracking-wide text-slate-500 uppercase">
              <span className={cx('size-2 rounded-full', TARJETA_ESTADO[i.key].punto)} aria-hidden="true" />
              {i.label}
            </span>
            <span className="mt-2 block text-2xl font-bold text-slate-900 tabular-nums">
              {i.valor ?? <span className="inline-block h-7 w-10 animate-pulse rounded bg-slate-100" />}
            </span>
          </button>
        )
      })}
    </div>
  )
}

export function PedidosListPage() {
  const { filtro, actualizar, limpiar } = useFiltroEnUrl()
  const { esAdmin } = useAuth()
  const { notify } = useToast()
  const navigate = useNavigate()
  const location = useLocation()
  const resaltado = (location.state as { resaltar?: number } | null)?.resaltar

  const [data, setData] = useState<PagedResult<PedidoResumen> | null>(null)
  const [resumen, setResumen] = useState<PedidoResumenEstados | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [recarga, setRecarga] = useState(0)
  const [aEliminar, setAEliminar] = useState<PedidoResumen | null>(null)
  const [eliminando, setEliminando] = useState(false)

  const [busqueda, setBusqueda] = useState(filtro.buscar ?? '')
  const busquedaDebounced = useDebounce(busqueda)

  useEffect(() => {
    if (busquedaDebounced !== (filtro.buscar ?? '')) actualizar({ buscar: busquedaDebounced })
  }, [busquedaDebounced, filtro.buscar, actualizar])

  useEffect(() => {
    const controller = new AbortController()
    setCargando(true)
    setError(null)
    pedidosApi
      .listar(filtro, controller.signal)
      .then((r) => {
        // Si al eliminar se vació la última página, se retrocede una.
        if (r.items.length === 0 && r.page > 1 && r.totalCount > 0) actualizar({ page: r.totalPages }, false)
        else setData(r)
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(toApiError(e))
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })
    return () => controller.abort()
  }, [filtro, recarga, actualizar])

  // El resumen no depende de los filtros: sólo se recarga al entrar y tras eliminar.
  useEffect(() => {
    const controller = new AbortController()
    pedidosApi
      .resumen(controller.signal)
      .then(setResumen)
      .catch(() => {})
    return () => controller.abort()
  }, [recarga])

  const hayFiltros = !!(filtro.buscar || filtro.estado || filtro.desde || filtro.hasta)

  function limpiarTodo() {
    setBusqueda('')
    limpiar()
  }

  function ordenarPor(columna: Orden) {
    const misma = filtro.ordenarPor === columna
    actualizar({ ordenarPor: columna, descendente: misma ? !filtro.descendente : columna === 'fecha' || columna === 'total' })
  }

  async function confirmarEliminar() {
    if (!aEliminar) return
    setEliminando(true)
    try {
      await pedidosApi.eliminar(aEliminar.id)
      notify(`Pedido ${aEliminar.numeroPedido} eliminado. El número queda reservado.`)
      setAEliminar(null)
      setRecarga((n) => n + 1)
    } catch (e) {
      notify(toApiError(e).message, 'error')
    } finally {
      setEliminando(false)
    }
  }

  const encabezado = (col: Orden, texto: string, className?: string) => {
    const activo = filtro.ordenarPor === col
    return (
      <th scope="col" className={cx('px-4 py-3', className)} aria-sort={activo ? (filtro.descendente ? 'descending' : 'ascending') : 'none'}>
        <button onClick={() => ordenarPor(col)} className={cx('inline-flex items-center gap-1 uppercase hover:text-slate-900', activo && 'text-brand-700')}>
          {texto}
          <IconChevron className={cx('size-3.5 transition-transform', activo ? 'opacity-100' : 'opacity-0', activo && !filtro.descendente && 'rotate-180')} />
        </button>
      </th>
    )
  }

  const puedeEliminar = (p: PedidoResumen) => esAdmin && p.estado !== 'Entregado'
  const motivoNoEliminar = (p: PedidoResumen) =>
    !esAdmin ? 'Sólo un administrador puede eliminar pedidos' : p.estado === 'Entregado' ? 'Un pedido entregado no se puede eliminar' : 'Eliminar'

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Operaciones"
        titulo="Pedidos"
        descripcion={data ? `${data.totalCount} pedido${data.totalCount === 1 ? '' : 's'}${hayFiltros ? ' con los filtros aplicados' : ' vigentes'}` : 'Cargando…'}
        acciones={
          <Link to="/pedidos/nuevo">
            <Button variant="accent" icon={<IconPlus className="size-4" />}>
              Nuevo pedido
            </Button>
          </Link>
        }
      />

      <ResumenEstados resumen={resumen} estado={filtro.estado ?? ''} onEstado={(e) => actualizar({ estado: e })} />

      <Card className="flex flex-wrap items-center gap-3 p-4">
        <div className="relative min-w-60 flex-1">
          <IconSearch className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
          <Input aria-label="Buscar" placeholder="Buscar por número, cliente o documento…" value={busqueda} onChange={(e) => setBusqueda(e.target.value)} className="pl-9" />
        </div>
        <div className="flex items-center gap-2">
          <Input type="date" aria-label="Desde" value={filtro.desde} max={filtro.hasta || undefined} onChange={(e) => actualizar({ desde: e.target.value })} className="w-40" />
          <span className="text-slate-400">–</span>
          <Input type="date" aria-label="Hasta" value={filtro.hasta} min={filtro.desde || undefined} onChange={(e) => actualizar({ hasta: e.target.value })} className="w-40" />
        </div>
        {hayFiltros && (
          <Button variant="ghost" onClick={limpiarTodo}>
            Limpiar filtros
          </Button>
        )}
      </Card>

      {error ? (
        <Alert>
          <div className="flex items-center justify-between gap-4">
            <span>No se pudieron cargar los pedidos: {error.message}</span>
            <Button size="sm" variant="secondary" onClick={() => setRecarga((n) => n + 1)}>
              Reintentar
            </Button>
          </div>
        </Alert>
      ) : (
        <Card className="overflow-hidden">
          {/* Tabla (escritorio) */}
          <div className="relative hidden overflow-x-auto md:block">
            <table className="min-w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50/80 text-left text-xs font-semibold tracking-wide text-slate-500">
                <tr>
                  {encabezado('numero', 'Número')}
                  {encabezado('cliente', 'Cliente')}
                  {encabezado('fecha', 'Fecha')}
                  <th scope="col" className="px-4 py-3 text-center uppercase">
                    Líneas
                  </th>
                  {encabezado('total', 'Total', 'text-right')}
                  {encabezado('estado', 'Estado')}
                  <th scope="col" className="px-4 py-3">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody className={cx('divide-y divide-slate-100 transition-opacity', cargando && data && 'opacity-50')}>
                {!data && <SkeletonRows columnas={7} />}
                {data?.items.map((p) => (
                  <tr
                    key={p.id}
                    onClick={() => navigate(`/pedidos/${p.id}/editar`)}
                    className={cx('cursor-pointer transition-colors hover:bg-brand-50/40', p.id === resaltado && 'bg-accent-50/60')}
                  >
                    <td className="px-4 py-3 font-mono font-semibold whitespace-nowrap text-brand-700">{p.numeroPedido}</td>
                    <td className="px-4 py-3 font-medium text-slate-900">{p.cliente}</td>
                    <td className="px-4 py-3 whitespace-nowrap text-slate-600">{formatFecha(p.fecha)}</td>
                    <td className="px-4 py-3 text-center text-slate-600">{p.cantidadLineas}</td>
                    <td className="px-4 py-3 text-right font-semibold whitespace-nowrap text-slate-900 tabular-nums">{formatMoney(p.total, p.moneda)}</td>
                    <td className="px-4 py-3">
                      <EstadoBadge estado={p.estado} />
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                      <Link
                        to={`/pedidos/${p.id}/editar`}
                        className="inline-flex rounded-lg p-2 text-slate-500 hover:bg-brand-50 hover:text-brand-700"
                        title={p.estado === 'Entregado' ? 'Ver' : 'Editar'}
                        aria-label={`Editar ${p.numeroPedido}`}
                      >
                        <IconPencil className="size-4" />
                      </Link>
                      <button
                        onClick={() => setAEliminar(p)}
                        disabled={!puedeEliminar(p)}
                        title={motivoNoEliminar(p)}
                        aria-label={`Eliminar ${p.numeroPedido}`}
                        className="inline-flex rounded-lg p-2 text-slate-500 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-slate-500"
                      >
                        <IconTrash className="size-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {cargando && data && (
              <div className="absolute inset-0 grid place-items-center">
                <Spinner className="size-6 text-brand-600" />
              </div>
            )}
          </div>

          {/* Tarjetas (móvil) */}
          <ul className="divide-y divide-slate-100 md:hidden">
            {!data && (
              <li className="grid place-items-center p-8">
                <Spinner className="size-6 text-brand-600" />
              </li>
            )}
            {data?.items.map((p) => (
              <li key={p.id} className={cx('flex items-center gap-3 p-4', p.id === resaltado && 'bg-accent-50/60')}>
                <Link to={`/pedidos/${p.id}/editar`} className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono font-semibold text-brand-700">{p.numeroPedido}</span>
                    <span className="font-semibold tabular-nums">{formatMoney(p.total, p.moneda)}</span>
                  </div>
                  <p className="truncate text-sm text-slate-700">{p.cliente}</p>
                  <div className="mt-1 flex items-center gap-2 text-xs text-slate-500">
                    {formatFecha(p.fecha)} · <EstadoBadge estado={p.estado} />
                  </div>
                </Link>
                {puedeEliminar(p) && (
                  <button onClick={() => setAEliminar(p)} className="rounded-lg p-2 text-slate-400 hover:text-red-600" aria-label={`Eliminar ${p.numeroPedido}`}>
                    <IconTrash className="size-5" />
                  </button>
                )}
              </li>
            ))}
          </ul>

          {data && data.items.length === 0 && (
            <EmptyState
              titulo={hayFiltros ? 'Ningún pedido coincide con los filtros' : 'Todavía no hay pedidos'}
              descripcion={hayFiltros ? 'Prueba con otro término o limpia los filtros.' : 'Registra el primero para empezar.'}
              accion={
                hayFiltros ? (
                  <Button variant="secondary" onClick={limpiarTodo}>
                    Limpiar filtros
                  </Button>
                ) : (
                  <Link to="/pedidos/nuevo">
                    <Button variant="accent" icon={<IconPlus className="size-4" />}>
                      Nuevo pedido
                    </Button>
                  </Link>
                )
              }
            />
          )}

          {data && <Paginacion {...data} onPage={(page) => actualizar({ page }, false)} onPageSize={(pageSize) => actualizar({ pageSize })} />}
        </Card>
      )}

      <ConfirmDialog
        open={!!aEliminar}
        title={`¿Eliminar el pedido ${aEliminar?.numeroPedido}?`}
        confirmLabel="Eliminar pedido"
        loading={eliminando}
        onConfirm={confirmarEliminar}
        onCancel={() => setAEliminar(null)}
      >
        <p>
          El pedido de <strong>{aEliminar?.cliente}</strong> por <strong>{aEliminar && formatMoney(aEliminar.total, aEliminar.moneda)}</strong> pasará a
          estado <strong>Anulado</strong> y dejará de aparecer en la lista.
        </p>
        <p className="mt-2 text-slate-500">Es una eliminación lógica: el registro se conserva y su número no podrá reutilizarse.</p>
      </ConfirmDialog>
    </div>
  )
}
