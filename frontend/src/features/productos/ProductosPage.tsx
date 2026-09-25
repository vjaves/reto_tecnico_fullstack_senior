import { useState } from 'react'
import { toApiError } from '../../api/http'
import { productosApi } from '../../api/services'
import { useAuth } from '../../auth/AuthContext'
import { IconCube, IconPencil, IconPlus, IconSearch, IconTrash } from '../../components/icons'
import { useToast } from '../../components/Toast'
import { ActivoBadge, Alert, Button, Card, ConfirmDialog, EmptyState, Input, PageHeader, Paginacion, Segmented, SkeletonRows, cx } from '../../components/ui'
import { useListadoMaestro } from '../../hooks/useListadoMaestro'
import type { FiltroActivo, ProductoResumen } from '../../types'
import { ProductoFormDrawer } from './ProductoFormDrawer'

const ESTADOS: { value: FiltroActivo; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'activos', label: 'Activos' },
  { value: 'inactivos', label: 'Inactivos' },
]

export function ProductosPage() {
  const { filtro, actualizar, busqueda, setBusqueda, data, cargando, error, recargar, limpiar } = useListadoMaestro(productosApi.listar)
  const { esAdmin } = useAuth()
  const { notify } = useToast()

  const [editando, setEditando] = useState<{ id: number | null } | null>(null)
  const [aEliminar, setAEliminar] = useState<ProductoResumen | null>(null)
  const [eliminando, setEliminando] = useState(false)
  const [resaltado, setResaltado] = useState<number | null>(null)

  const hayFiltros = !!(filtro.buscar || filtro.estado)

  async function eliminar() {
    if (!aEliminar) return
    setEliminando(true)
    try {
      await productosApi.eliminar(aEliminar.id)
      notify(`Producto ${aEliminar.codigo ?? aEliminar.nombre} eliminado.`)
      setAEliminar(null)
      recargar()
    } catch (e) {
      notify(toApiError(e).message, 'error')
      setAEliminar(null)
    } finally {
      setEliminando(false)
    }
  }

  const motivoNoEliminar = (p: ProductoResumen) =>
    !esAdmin ? 'Sólo un administrador puede eliminar' : p.pedidosEnCurso > 0 ? `Está en ${p.pedidosEnCurso} pedido(s) en curso` : 'Eliminar'

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Maestros"
        titulo="Productos"
        descripcion={data ? `${data.totalCount} producto${data.totalCount === 1 ? '' : 's'}${hayFiltros ? ' con los filtros aplicados' : ''}` : 'Cargando…'}
        acciones={
          <Button variant="accent" icon={<IconPlus className="size-4" />} onClick={() => setEditando({ id: null })}>
            Nuevo producto
          </Button>
        }
      />

      <Card className="flex flex-wrap items-center gap-3 p-4">
        <div className="relative min-w-60 flex-1">
          <IconSearch className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
          <Input aria-label="Buscar" placeholder="Buscar por código o nombre…" value={busqueda} onChange={(e) => setBusqueda(e.target.value)} className="pl-9" />
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
            <span>No se pudieron cargar los productos: {error.message}</span>
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
                  <th className="px-4 py-3">Código</th>
                  <th className="px-4 py-3">Producto</th>
                  <th className="hidden px-4 py-3 md:table-cell">Unidad</th>
                  <th className="hidden px-4 py-3 text-center sm:table-cell">Pedidos en curso</th>
                  <th className="px-4 py-3">Estado</th>
                  <th className="px-4 py-3">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody className={cx('divide-y divide-slate-100 transition-opacity', cargando && data && 'opacity-50')}>
                {!data && <SkeletonRows columnas={6} />}
                {data?.items.map((p) => (
                  <tr
                    key={p.id}
                    onClick={() => setEditando({ id: p.id })}
                    className={cx('cursor-pointer transition-colors hover:bg-brand-50/40', p.id === resaltado && 'bg-accent-50/60')}
                  >
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className="rounded-md bg-brand-50 px-2 py-1 font-mono text-xs font-semibold text-brand-700">{p.codigo ?? '—'}</span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-500" aria-hidden="true">
                          <IconCube className="size-4" />
                        </span>
                        <span className="font-medium text-slate-900">{p.nombre}</span>
                      </div>
                    </td>
                    <td className="hidden px-4 py-3 text-slate-600 md:table-cell">{p.unidadMedida ?? '—'}</td>
                    <td className="hidden px-4 py-3 text-center sm:table-cell">
                      {p.pedidosEnCurso > 0 ? (
                        <span className="rounded-full bg-accent-50 px-2 py-0.5 text-xs font-semibold text-accent-700 ring-1 ring-accent-200">{p.pedidosEnCurso}</span>
                      ) : (
                        <span className="text-slate-400">0</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <ActivoBadge activo={p.activo} />
                    </td>
                    <td className="px-4 py-3 text-right whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                      <button
                        onClick={() => setEditando({ id: p.id })}
                        className="inline-flex rounded-lg p-2 text-slate-500 hover:bg-brand-50 hover:text-brand-700"
                        aria-label={`Editar ${p.nombre}`}
                        title="Editar"
                      >
                        <IconPencil className="size-4" />
                      </button>
                      <button
                        onClick={() => setAEliminar(p)}
                        disabled={!esAdmin || p.pedidosEnCurso > 0}
                        title={motivoNoEliminar(p)}
                        aria-label={`Eliminar ${p.nombre}`}
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
              titulo={hayFiltros ? 'Ningún producto coincide con la búsqueda' : 'Todavía no hay productos'}
              descripcion={hayFiltros ? 'Prueba con otro término o limpia los filtros.' : 'Registra el primero para poder usarlo en pedidos.'}
              accion={
                hayFiltros ? (
                  <Button variant="secondary" onClick={limpiar}>
                    Limpiar filtros
                  </Button>
                ) : (
                  <Button variant="accent" icon={<IconPlus className="size-4" />} onClick={() => setEditando({ id: null })}>
                    Nuevo producto
                  </Button>
                )
              }
            />
          )}

          {data && <Paginacion {...data} onPage={(page) => actualizar({ page })} onPageSize={(pageSize) => actualizar({ pageSize })} />}
        </Card>
      )}

      <ProductoFormDrawer
        open={!!editando}
        productoId={editando?.id ?? null}
        onClose={() => setEditando(null)}
        onSaved={(p, esNuevo) => {
          notify(esNuevo ? `Producto ${p.codigo} registrado.` : 'Producto actualizado.')
          setEditando(null)
          setResaltado(p.id)
          recargar()
        }}
      />

      <ConfirmDialog
        open={!!aEliminar}
        title={`¿Eliminar ${aEliminar?.codigo ?? ''} ${aEliminar?.nombre ?? ''}?`}
        confirmLabel="Eliminar producto"
        loading={eliminando}
        onConfirm={eliminar}
        onCancel={() => setAEliminar(null)}
      >
        <p>El producto dejará de ofrecerse en pedidos nuevos.</p>
        <p className="mt-2 text-slate-500">Si sólo quieres dejar de venderlo temporalmente, desactívalo en lugar de eliminarlo.</p>
      </ConfirmDialog>
    </div>
  )
}
