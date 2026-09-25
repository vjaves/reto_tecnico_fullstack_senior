import { useState } from 'react'
import { toApiError } from '../../api/http'
import { clientesApi } from '../../api/services'
import { useAuth } from '../../auth/AuthContext'
import { IconPencil, IconPlus, IconSearch, IconTrash } from '../../components/icons'
import { useToast } from '../../components/Toast'
import { ActivoBadge, Alert, Button, Card, ConfirmDialog, EmptyState, Input, PageHeader, Paginacion, Segmented, SkeletonRows, cx } from '../../components/ui'
import { useListadoMaestro } from '../../hooks/useListadoMaestro'
import type { ClienteResumen, FiltroActivo } from '../../types'
import { ClienteFormDrawer } from './ClienteFormDrawer'

const ESTADOS: { value: FiltroActivo; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'activos', label: 'Activos' },
  { value: 'inactivos', label: 'Inactivos' },
]

const iniciales = (nombre: string) =>
  nombre
    .split(/\s+/)
    .filter((p) => /^[A-Za-zÁÉÍÓÚÑáéíóúñ]/.test(p))
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase()

export function ClientesPage() {
  const { filtro, actualizar, busqueda, setBusqueda, data, cargando, error, recargar, limpiar } = useListadoMaestro(clientesApi.listar)
  const { esAdmin } = useAuth()
  const { notify } = useToast()

  const [editando, setEditando] = useState<{ id: number | null } | null>(null)
  const [aEliminar, setAEliminar] = useState<ClienteResumen | null>(null)
  const [eliminando, setEliminando] = useState(false)
  const [resaltado, setResaltado] = useState<number | null>(null)

  const hayFiltros = !!(filtro.buscar || filtro.estado)

  async function eliminar() {
    if (!aEliminar) return
    setEliminando(true)
    try {
      await clientesApi.eliminar(aEliminar.id)
      notify(`Cliente ${aEliminar.nombre} eliminado.`)
      setAEliminar(null)
      recargar()
    } catch (e) {
      notify(toApiError(e).message, 'error')
      setAEliminar(null)
    } finally {
      setEliminando(false)
    }
  }

  const motivoNoEliminar = (c: ClienteResumen) =>
    !esAdmin ? 'Sólo un administrador puede eliminar' : c.pedidosEnCurso > 0 ? `Tiene ${c.pedidosEnCurso} pedido(s) en curso` : 'Eliminar'

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Maestros"
        titulo="Clientes"
        descripcion={data ? `${data.totalCount} cliente${data.totalCount === 1 ? '' : 's'}${hayFiltros ? ' con los filtros aplicados' : ''}` : 'Cargando…'}
        acciones={
          <Button variant="accent" icon={<IconPlus className="size-4" />} onClick={() => setEditando({ id: null })}>
            Nuevo cliente
          </Button>
        }
      />

      <Card className="flex flex-wrap items-center gap-3 p-4">
        <div className="relative min-w-60 flex-1">
          <IconSearch className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
          <Input aria-label="Buscar" placeholder="Buscar por nombre, razón social o documento…" value={busqueda} onChange={(e) => setBusqueda(e.target.value)} className="pl-9" />
        </div>
        <Segmented label="Filtrar por estado" value={filtro.estado ?? ''} onChange={(v) => actualizar({ estado: v })} opciones={ESTADOS} />
        {hayFiltros && (
          <Button variant="ghost" onClick={limpiar}>
            Limpiar
          </Button>
        )}
      </Card>

      {error ? (
        <Alert>
          <div className="flex items-center justify-between gap-4">
            <span>No se pudieron cargar los clientes: {error.message}</span>
            <Button size="sm" variant="secondary" onClick={recargar}>
              Reintentar
            </Button>
          </div>
        </Alert>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50/80 text-left text-xs font-semibold tracking-wide text-slate-500 uppercase">
                <tr>
                  <th className="px-4 py-3">Cliente</th>
                  <th className="px-4 py-3">Documento</th>
                  <th className="hidden px-4 py-3 md:table-cell">Celular</th>
                  <th className="hidden px-4 py-3 text-center sm:table-cell">Pedidos en curso</th>
                  <th className="px-4 py-3">Estado</th>
                  <th className="px-4 py-3">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody className={cx('divide-y divide-slate-100 transition-opacity', cargando && data && 'opacity-50')}>
                {!data && <SkeletonRows columnas={6} />}
                {data?.items.map((c) => (
                  <tr
                    key={c.id}
                    onClick={() => setEditando({ id: c.id })}
                    className={cx('cursor-pointer transition-colors hover:bg-brand-50/40', c.id === resaltado && 'bg-accent-50/60')}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <span
                          className={cx(
                            'grid size-9 shrink-0 place-items-center rounded-full text-xs font-bold',
                            c.tipoDocumento === 'RUC' ? 'bg-brand-100 text-brand-700' : 'bg-accent-100 text-accent-700',
                          )}
                          aria-hidden="true"
                        >
                          {iniciales(c.nombre)}
                        </span>
                        <span className="font-medium text-slate-900">{c.nombre}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-semibold text-slate-600">{c.tipoDocumento}</span>
                      <span className="font-mono text-slate-700">{c.numeroDocumento}</span>
                    </td>
                    <td className="hidden px-4 py-3 text-slate-600 md:table-cell">{c.celular ?? '—'}</td>
                    <td className="hidden px-4 py-3 text-center sm:table-cell">
                      {c.pedidosEnCurso > 0 ? (
                        <span className="rounded-full bg-accent-50 px-2 py-0.5 text-xs font-semibold text-accent-700 ring-1 ring-accent-200">{c.pedidosEnCurso}</span>
                      ) : (
                        <span className="text-slate-400">0</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <ActivoBadge activo={c.activo} />
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                      <button
                        onClick={() => setEditando({ id: c.id })}
                        className="inline-flex rounded-lg p-2 text-slate-500 hover:bg-brand-50 hover:text-brand-700"
                        aria-label={`Editar ${c.nombre}`}
                        title="Editar"
                      >
                        <IconPencil className="size-4" />
                      </button>
                      <button
                        onClick={() => setAEliminar(c)}
                        disabled={!esAdmin || c.pedidosEnCurso > 0}
                        title={motivoNoEliminar(c)}
                        aria-label={`Eliminar ${c.nombre}`}
                        className="inline-flex rounded-lg p-2 text-slate-500 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-slate-500"
                      >
                        <IconTrash className="size-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {data && data.items.length === 0 && (
            <EmptyState
              titulo={hayFiltros ? 'Ningún cliente coincide con la búsqueda' : 'Todavía no hay clientes'}
              descripcion={hayFiltros ? 'Prueba con otro término o limpia los filtros.' : 'Registra el primero para poder tomarle pedidos.'}
              accion={
                hayFiltros ? (
                  <Button variant="secondary" onClick={limpiar}>
                    Limpiar filtros
                  </Button>
                ) : (
                  <Button variant="accent" icon={<IconPlus className="size-4" />} onClick={() => setEditando({ id: null })}>
                    Nuevo cliente
                  </Button>
                )
              }
            />
          )}

          {data && (
            <Paginacion
              {...data}
              onPage={(page) => actualizar({ page })}
              onPageSize={(pageSize) => actualizar({ pageSize })}
            />
          )}
        </Card>
      )}

      <ClienteFormDrawer
        open={!!editando}
        clienteId={editando?.id ?? null}
        onClose={() => setEditando(null)}
        onSaved={(c, esNuevo) => {
          notify(esNuevo ? `Cliente ${c.razonSocial ?? `${c.primerNombre} ${c.apellidoPaterno}`} registrado.` : 'Cliente actualizado.')
          setEditando(null)
          setResaltado(c.id)
          recargar()
        }}
      />

      <ConfirmDialog
        open={!!aEliminar}
        title={`¿Eliminar a ${aEliminar?.nombre}?`}
        confirmLabel="Eliminar cliente"
        loading={eliminando}
        onConfirm={eliminar}
        onCancel={() => setAEliminar(null)}
      >
        <p>El cliente dejará de aparecer en la lista y al registrar pedidos.</p>
        <p className="mt-2 text-slate-500">Es una baja lógica: sus pedidos históricos se conservan.</p>
      </ConfirmDialog>
    </div>
  )
}
