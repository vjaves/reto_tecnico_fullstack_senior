import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useBeforeUnload, useBlocker, useNavigate, useParams } from 'react-router'
import { toApiError } from '../api/http'
import { catalogosApi, pedidosApi } from '../api/services'
import { useAuth } from '../auth/AuthContext'
import { ClienteCombobox } from '../components/ClienteCombobox'
import { ClienteFormDrawer } from '../features/clientes/ClienteFormDrawer'
import { IconPlus, IconTrash } from '../components/icons'
import { useToast } from '../components/Toast'
import { Alert, Button, Card, ConfirmDialog, EstadoBadge, Field, Input, Select, Spinner, cx } from '../components/ui'
import { ESTADOS_EDITABLES, type Cliente, type EstadoPedido, type ItemCatalogo, type Pedido, type PedidoRequest, type Producto } from '../types'
import { formatMoney, hoyIso, redondear2 } from '../utils/format'

interface LineaForm {
  key: string
  id: number | null
  productoId: number
  cantidad: string
  precioVenta: string
  descuento: string
}

interface FormState {
  numeroPedido: string
  clienteId: number
  sucursalId: number
  monedaId: number
  fecha: string
  estado: EstadoPedido
  lineas: LineaForm[]
}

interface Catalogos {
  clientes: Cliente[]
  productos: Producto[]
  monedas: ItemCatalogo[]
  sucursales: ItemCatalogo[]
}

let secuencia = 0
const nuevaLinea = (): LineaForm => ({ key: `n${++secuencia}`, id: null, productoId: 0, cantidad: '1', precioVenta: '', descuento: '' })

const num = (s: string) => {
  const n = Number(s)
  return Number.isFinite(n) ? n : 0
}

function calcularLinea(l: LineaForm) {
  const subTotal = redondear2(num(l.cantidad) * num(l.precioVenta))
  const descuento = num(l.descuento)
  return { subTotal, descuento, importe: redondear2(subTotal - descuento) }
}

function desdePedido(p: Pedido): FormState {
  return {
    numeroPedido: p.numeroPedido,
    clienteId: p.clienteId,
    sucursalId: p.sucursalId,
    monedaId: p.monedaId,
    fecha: p.fecha,
    estado: p.estado,
    lineas: p.detalles.map((d) => ({
      key: `d${d.id}`,
      id: d.id,
      productoId: d.productoId,
      cantidad: String(d.cantidad),
      precioVenta: String(d.precioVenta),
      descuento: d.descuento ? String(d.descuento) : '',
    })),
  }
}

/** Mismas reglas que el backend, para avisar antes de enviar. Las claves coinciden con las del ProblemDetails. */
function validar(f: FormState): Record<string, string> {
  const e: Record<string, string> = {}
  if (!f.numeroPedido.trim()) e.numeroPedido = 'El número de pedido es obligatorio.'
  else if (!/^[A-Za-z0-9-]{1,20}$/.test(f.numeroPedido.trim())) e.numeroPedido = 'Sólo letras, números y guiones (máx. 20).'
  if (!f.clienteId) e.clienteId = 'Seleccione un cliente.'
  if (!f.sucursalId) e.sucursalId = 'Seleccione una sucursal.'
  if (!f.monedaId) e.monedaId = 'Seleccione una moneda.'
  if (!f.fecha) e.fecha = 'La fecha es obligatoria.'
  if (f.lineas.length === 0) e.detalles = 'El pedido debe tener al menos una línea.'

  f.lineas.forEach((l, i) => {
    const { subTotal, descuento } = calcularLinea(l)
    if (!l.productoId) e[`detalles[${i}].productoId`] = 'Seleccione un producto.'
    if (!(num(l.cantidad) > 0)) e[`detalles[${i}].cantidad`] = 'Debe ser mayor a 0.'
    if (l.precioVenta === '' || num(l.precioVenta) < 0) e[`detalles[${i}].precioVenta`] = 'Precio inválido.'
    if (descuento < 0) e[`detalles[${i}].descuento`] = 'No puede ser negativo.'
    else if (descuento > subTotal) e[`detalles[${i}].descuento`] = 'Supera el subtotal.'
  })
  return e
}

export function PedidoFormPage() {
  const { id } = useParams()
  const pedidoId = id ? Number(id) : null
  const esEdicion = pedidoId !== null
  const navigate = useNavigate()
  const { notify } = useToast()
  const { esAdmin } = useAuth()

  const [catalogos, setCatalogos] = useState<Catalogos | null>(null)
  const [pedido, setPedido] = useState<Pedido | null>(null)
  const [form, setForm] = useState<FormState | null>(null)
  const [inicial, setInicial] = useState<string>('')
  const [cargaError, setCargaError] = useState<string | null>(null)

  const [intentoGuardar, setIntentoGuardar] = useState(false)
  const [guardando, setGuardando] = useState(false)
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [erroresServidor, setErroresServidor] = useState<Record<string, string>>({})
  const [conflicto, setConflicto] = useState(false)
  const [numeroSugerido, setNumeroSugerido] = useState<string | null>(null)
  const [confirmarEliminar, setConfirmarEliminar] = useState(false)
  const [nuevoCliente, setNuevoCliente] = useState(false)
  const [eliminando, setEliminando] = useState(false)
  const salirSinPreguntar = useRef(false)

  const cargar = useCallback(async () => {
    setCargaError(null)
    try {
      const [clientes, productos, monedas, sucursales, base] = await Promise.all([
        catalogosApi.clientes(),
        catalogosApi.productos(),
        catalogosApi.monedas(),
        catalogosApi.sucursales(),
        pedidoId !== null ? pedidosApi.obtener(pedidoId) : pedidosApi.siguienteNumero(),
      ])
      setCatalogos({ clientes, productos, monedas, sucursales })

      const estado: FormState =
        typeof base === 'string'
          ? {
              numeroPedido: base,
              clienteId: 0,
              sucursalId: sucursales[0]?.id ?? 0,
              monedaId: monedas[0]?.id ?? 0,
              fecha: hoyIso(),
              estado: 'Registrado',
              lineas: [nuevaLinea()],
            }
          : desdePedido(base)
      if (typeof base !== 'string') setPedido(base)
      setForm(estado)
      setInicial(JSON.stringify(estado))
      setErroresServidor({})
      setErrorGeneral(null)
      setConflicto(false)
    } catch (e) {
      const err = toApiError(e)
      setCargaError(err.status === 404 ? 'El pedido no existe o fue eliminado.' : err.message)
    }
  }, [pedidoId])

  useEffect(() => {
    void cargar()
  }, [cargar])

  const soloLectura = pedido?.estado === 'Entregado'
  const sucio = !!form && JSON.stringify(form) !== inicial
  const moneda = catalogos?.monedas.find((m) => m.id === form?.monedaId)?.abreviatura ?? 'PEN'

  const erroresCliente = useMemo(() => (form ? validar(form) : {}), [form])
  const errores = { ...(intentoGuardar ? erroresCliente : {}), ...erroresServidor }
  const err = (clave: string) => errores[clave]

  const totales = useMemo(() => {
    const lineas = form?.lineas.map(calcularLinea) ?? []
    return {
      subTotal: redondear2(lineas.reduce((s, l) => s + l.subTotal, 0)),
      descuentos: redondear2(lineas.reduce((s, l) => s + l.descuento, 0)),
      total: redondear2(lineas.reduce((s, l) => s + l.importe, 0)),
    }
  }, [form])

  // Avisos de cambios sin guardar: navegación interna (useBlocker) y cierre de pestaña (beforeunload).
  const blocker = useBlocker(() => sucio && !salirSinPreguntar.current)
  useBeforeUnload(
    useCallback(
      (e: BeforeUnloadEvent) => {
        if (sucio && !salirSinPreguntar.current) e.preventDefault()
      },
      [sucio],
    ),
  )

  function set<K extends keyof FormState>(campo: K, valor: FormState[K]) {
    setForm((f) => (f ? { ...f, [campo]: valor } : f))
    setErroresServidor((e) => {
      const { [campo as string]: _, ...resto } = e
      return resto
    })
  }

  function setLinea(i: number, cambios: Partial<LineaForm>) {
    setForm((f) => (f ? { ...f, lineas: f.lineas.map((l, j) => (j === i ? { ...l, ...cambios } : l)) } : f))
    setErroresServidor((e) => Object.fromEntries(Object.entries(e).filter(([k]) => !k.startsWith(`detalles[${i}]`))))
  }

  async function guardar(e?: FormEvent) {
    e?.preventDefault()
    if (!form || soloLectura || guardando) return
    setIntentoGuardar(true)
    setErrorGeneral(null)
    setNumeroSugerido(null)
    if (Object.keys(validar(form)).length > 0 || totales.total <= 0) {
      if (totales.total <= 0 && form.lineas.length > 0) setErrorGeneral('El total del pedido debe ser mayor a 0.')
      return
    }

    const payload: PedidoRequest = {
      numeroPedido: form.numeroPedido.trim().toUpperCase(),
      clienteId: form.clienteId,
      sucursalId: form.sucursalId,
      monedaId: form.monedaId,
      fecha: form.fecha,
      detalles: form.lineas.map((l) => ({
        id: l.id,
        productoId: l.productoId,
        cantidad: num(l.cantidad),
        precioVenta: num(l.precioVenta),
        descuento: num(l.descuento),
      })),
      ...(esEdicion ? { estado: form.estado, rowVersion: pedido?.rowVersion } : {}),
    }

    setGuardando(true)
    try {
      const guardado = pedidoId !== null ? await pedidosApi.actualizar(pedidoId, payload) : await pedidosApi.crear(payload)
      notify(esEdicion ? `Pedido ${guardado.numeroPedido} actualizado.` : `Pedido ${guardado.numeroPedido} registrado por ${formatMoney(guardado.total, guardado.moneda)}.`)
      salirSinPreguntar.current = true
      navigate('/pedidos', { state: { resaltar: guardado.id } })
    } catch (e) {
      const apiError = toApiError(e)
      const campos = Object.fromEntries(Object.entries(apiError.fieldErrors).map(([k, v]) => [k, v[0]]))
      setErroresServidor(campos)

      if (apiError.code === 'PEDIDO_NUMERO_DUPLICADO') {
        setErroresServidor({ ...campos, numeroPedido: apiError.message })
        pedidosApi.siguienteNumero().then(setNumeroSugerido).catch(() => {})
      } else if (apiError.code === 'PEDIDO_MODIFICADO') {
        setConflicto(true)
      } else if (Object.keys(campos).length === 0 || apiError.status !== 400) {
        setErrorGeneral(apiError.message)
      }
    } finally {
      setGuardando(false)
    }
  }

  // Ctrl+S / Cmd+S guarda.
  const guardarRef = useRef(guardar)
  useEffect(() => {
    guardarRef.current = guardar
  })
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        void guardarRef.current()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  async function eliminar() {
    if (!pedido) return
    setEliminando(true)
    try {
      await pedidosApi.eliminar(pedido.id)
      notify(`Pedido ${pedido.numeroPedido} eliminado.`)
      salirSinPreguntar.current = true
      navigate('/pedidos')
    } catch (e) {
      notify(toApiError(e).message, 'error')
      setConfirmarEliminar(false)
    } finally {
      setEliminando(false)
    }
  }

  if (cargaError) {
    return (
      <div className="mx-auto max-w-xl space-y-4">
        <Alert>{cargaError}</Alert>
        <Link to="/pedidos" className="text-sm font-medium text-brand-700 hover:underline">
          ← Volver a pedidos
        </Link>
      </div>
    )
  }

  if (!form || !catalogos) {
    return (
      <div className="grid place-items-center py-24 text-brand-600">
        <Spinner className="size-8" />
      </div>
    )
  }

  const estadosPermitidos = ESTADOS_EDITABLES.filter(
    (e) => !pedido || ESTADOS_EDITABLES.indexOf(e) >= ESTADOS_EDITABLES.indexOf(pedido.estado),
  )

  return (
    <form onSubmit={guardar} noValidate className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link to="/pedidos" className="text-xs font-semibold tracking-wider text-accent-600 uppercase hover:text-accent-700">
            ← Pedidos
          </Link>
          <h1 className="mt-1 flex items-center gap-3 text-2xl font-bold tracking-tight text-slate-900">
            {esEdicion ? `Pedido ${pedido?.numeroPedido}` : 'Nuevo pedido'}
            {pedido && <EstadoBadge estado={pedido.estado} />}
          </h1>
          {pedido?.fechaModificacion && (
            <p className="mt-1 text-xs text-slate-500">Última modificación: {new Date(pedido.fechaModificacion).toLocaleString('es-PE')}</p>
          )}
        </div>
        <div className="flex gap-2">
          {esEdicion && esAdmin && !soloLectura && (
            <Button variant="secondary" className="text-red-600" icon={<IconTrash className="size-4" />} onClick={() => setConfirmarEliminar(true)}>
              Eliminar
            </Button>
          )}
          {!soloLectura && (
            <Button type="submit" loading={guardando} title="Ctrl + S">
              {esEdicion ? 'Guardar cambios' : 'Registrar pedido'}
            </Button>
          )}
        </div>
      </div>

      {soloLectura && <Alert tone="info">Este pedido ya fue entregado: se muestra sólo para consulta.</Alert>}
      {conflicto && (
        <Alert tone="warning">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span>Otro usuario modificó este pedido mientras lo editabas. Recarga para ver la versión actual (perderás tus cambios).</span>
            <Button size="sm" variant="secondary" onClick={() => void cargar()}>
              Recargar datos
            </Button>
          </div>
        </Alert>
      )}
      {errorGeneral && <Alert>{errorGeneral}</Alert>}

      <fieldset disabled={soloLectura} className="space-y-6">
        <Card className="grid gap-5 p-6 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Número de pedido" error={err('numeroPedido')} hint={!esEdicion ? 'Sugerido automáticamente; puedes cambiarlo.' : undefined}>
            {(fid, desc) => (
              <>
                <Input
                  id={fid}
                  aria-describedby={desc}
                  value={form.numeroPedido}
                  maxLength={20}
                  invalid={!!err('numeroPedido')}
                  onChange={(e) => set('numeroPedido', e.target.value.toUpperCase())}
                  className="font-mono"
                />
                {numeroSugerido && (
                  <button type="button" className="mt-1 text-xs font-medium text-brand-700 hover:underline" onClick={() => set('numeroPedido', numeroSugerido)}>
                    Usar {numeroSugerido}
                  </button>
                )}
              </>
            )}
          </Field>

          <Field label="Fecha" error={err('fecha')}>
            {(fid, desc) => (
              <Input id={fid} aria-describedby={desc} type="date" value={form.fecha} invalid={!!err('fecha')} onChange={(e) => set('fecha', e.target.value)} />
            )}
          </Field>

          <Field label="Sucursal" error={err('sucursalId')}>
            {(fid, desc) => (
              <Select id={fid} aria-describedby={desc} value={form.sucursalId} invalid={!!err('sucursalId')} onChange={(e) => set('sucursalId', Number(e.target.value))}>
                <option value={0}>Seleccione…</option>
                {catalogos.sucursales.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.nombre}
                  </option>
                ))}
              </Select>
            )}
          </Field>

          <Field label="Moneda" error={err('monedaId')}>
            {(fid, desc) => (
              <Select id={fid} aria-describedby={desc} value={form.monedaId} invalid={!!err('monedaId')} onChange={(e) => set('monedaId', Number(e.target.value))}>
                {catalogos.monedas.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.nombre} ({m.abreviatura})
                  </option>
                ))}
              </Select>
            )}
          </Field>

          <Field label="Cliente" required error={err('clienteId')} className="sm:col-span-2 lg:col-span-3">
            {(fid, desc) => (
              <>
                <ClienteCombobox
                  id={fid}
                  describedBy={desc}
                  clientes={catalogos.clientes}
                  value={form.clienteId}
                  invalid={!!err('clienteId')}
                  disabled={soloLectura}
                  onChange={(v) => set('clienteId', v)}
                />
                {!soloLectura && (
                  <button
                    type="button"
                    onClick={() => setNuevoCliente(true)}
                    className="mt-1.5 inline-flex items-center gap-1 text-xs font-semibold text-accent-600 hover:text-accent-700"
                  >
                    <IconPlus className="size-3.5" />
                    ¿Cliente nuevo? Regístralo aquí
                  </button>
                )}
              </>
            )}
          </Field>

          {esEdicion && (
            <Field label="Estado" error={err('estado')} hint="Un pedido sólo avanza de estado.">
              {(fid, desc) => (
                <Select id={fid} aria-describedby={desc} value={form.estado} onChange={(e) => set('estado', e.target.value as EstadoPedido)}>
                  {(soloLectura ? [form.estado] : estadosPermitidos).map((e) => (
                    <option key={e}>{e}</option>
                  ))}
                </Select>
              )}
            </Field>
          )}
        </Card>

        <Card>
          <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
            <h2 className="flex items-center gap-2 font-semibold text-slate-900">
              <span className="h-4 w-1 rounded-full bg-accent-600" aria-hidden="true" />
              Líneas del pedido
            </h2>
            {!soloLectura && (
              <Button size="sm" variant="secondary" icon={<IconPlus className="size-4" />} onClick={() => set('lineas', [...form.lineas, nuevaLinea()])}>
                Agregar línea
              </Button>
            )}
          </div>

          {err('detalles') && (
            <div className="px-5 pt-3">
              <Alert>{err('detalles')}</Alert>
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-slate-50/80 text-left text-xs font-semibold tracking-wide text-slate-500 uppercase">
                <tr>
                  <th className="w-10 px-3 py-2 text-center">#</th>
                  <th className="min-w-64 px-3 py-2">Producto</th>
                  <th className="w-28 px-3 py-2 text-right">Cantidad</th>
                  <th className="w-36 px-3 py-2 text-right">Precio unit.</th>
                  <th className="w-32 px-3 py-2 text-right">Descuento</th>
                  <th className="w-36 px-3 py-2 text-right">Importe</th>
                  <th className="w-12 px-3 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {form.lineas.map((l, i) => {
                  const calc = calcularLinea(l)
                  const e = (campo: string) => err(`detalles[${i}].${campo}`)
                  return (
                    <tr key={l.key} className="align-top">
                      <td className="px-3 py-2.5 text-center text-slate-400">{i + 1}</td>
                      <td className="px-3 py-2">
                        <Select aria-label={`Producto línea ${i + 1}`} value={l.productoId} invalid={!!e('productoId')} onChange={(ev) => setLinea(i, { productoId: Number(ev.target.value) })}>
                          <option value={0}>Seleccione un producto…</option>
                          {catalogos.productos.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.codigo ? `${p.codigo} · ` : ''}
                              {p.nombre}
                            </option>
                          ))}
                        </Select>
                        {e('productoId') && <p className="mt-1 text-xs text-red-600">{e('productoId')}</p>}
                      </td>
                      {(['cantidad', 'precioVenta', 'descuento'] as const).map((campo) => (
                        <td key={campo} className="px-3 py-2">
                          <Input
                            aria-label={`${campo} línea ${i + 1}`}
                            type="number"
                            inputMode="decimal"
                            min={0}
                            step={campo === 'cantidad' ? '1' : '0.01'}
                            placeholder={campo === 'descuento' ? '0.00' : campo === 'precioVenta' ? '0.00' : ''}
                            value={l[campo]}
                            invalid={!!e(campo)}
                            onChange={(ev) => setLinea(i, { [campo]: ev.target.value })}
                            className="text-right"
                          />
                          {e(campo) && <p className="mt-1 text-right text-xs text-red-600">{e(campo)}</p>}
                        </td>
                      ))}
                      <td className="px-3 py-2.5 text-right font-medium tabular-nums text-slate-900">
                        {formatMoney(calc.importe, moneda)}
                        {calc.descuento > 0 && <p className="text-xs font-normal text-slate-400 line-through">{formatMoney(calc.subTotal, moneda)}</p>}
                      </td>
                      <td className="px-3 py-2">
                        {!soloLectura && (
                          <button
                            type="button"
                            onClick={() => set('lineas', form.lineas.filter((_, j) => j !== i))}
                            disabled={form.lineas.length === 1}
                            title={form.lineas.length === 1 ? 'El pedido necesita al menos una línea' : 'Quitar línea'}
                            aria-label={`Quitar línea ${i + 1}`}
                            className="rounded-lg p-2 text-slate-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-slate-400"
                          >
                            <IconTrash className="size-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          <div className="flex justify-end border-t border-slate-200 px-5 py-4">
            <dl className="w-full max-w-xs space-y-1 text-sm">
              <div className="flex justify-between text-slate-600">
                <dt>Subtotal</dt>
                <dd className="tabular-nums">{formatMoney(totales.subTotal, moneda)}</dd>
              </div>
              <div className="flex justify-between text-slate-600">
                <dt>Descuentos</dt>
                <dd className="tabular-nums">− {formatMoney(totales.descuentos, moneda)}</dd>
              </div>
              <div className={cx('flex justify-between border-t border-slate-200 pt-2 text-base font-semibold', totales.total <= 0 ? 'text-red-600' : 'text-slate-900')}>
                <dt>Total</dt>
                <dd className="tabular-nums">{formatMoney(totales.total, moneda)}</dd>
              </div>
              {totales.total <= 0 && <p className="text-right text-xs text-red-600">El total debe ser mayor a 0 para guardar.</p>}
            </dl>
          </div>
        </Card>
      </fieldset>

      {!soloLectura && (
        <div className="flex items-center justify-end gap-3">
          <span className="hidden text-xs text-slate-400 sm:inline">{sucio ? 'Cambios sin guardar · Ctrl+S para guardar' : 'Sin cambios'}</span>
          <Button variant="secondary" onClick={() => navigate('/pedidos')}>
            Cancelar
          </Button>
          <Button type="submit" loading={guardando}>
            {esEdicion ? 'Guardar cambios' : 'Registrar pedido'}
          </Button>
        </div>
      )}

      <ClienteFormDrawer
        open={nuevoCliente}
        clienteId={null}
        onClose={() => setNuevoCliente(false)}
        onSaved={async (c) => {
          setNuevoCliente(false)
          const clientes = await catalogosApi.clientes()
          setCatalogos((cat) => (cat ? { ...cat, clientes } : cat))
          if (c.activo) set('clienteId', c.id)
          notify(c.activo ? 'Cliente registrado y seleccionado.' : 'Cliente registrado como inactivo: no se puede usar en pedidos.', c.activo ? 'success' : 'info')
        }}
      />

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        title="¿Descartar los cambios?"
        confirmLabel="Descartar y salir"
        onConfirm={() => blocker.proceed?.()}
        onCancel={() => blocker.reset?.()}
      >
        Tienes cambios sin guardar en este pedido. Si sales ahora, se perderán.
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmarEliminar}
        title={`¿Eliminar el pedido ${pedido?.numeroPedido}?`}
        confirmLabel="Eliminar pedido"
        loading={eliminando}
        onConfirm={eliminar}
        onCancel={() => setConfirmarEliminar(false)}
      >
        El pedido pasará a estado Anulado y dejará de aparecer en la lista. El número queda reservado.
      </ConfirmDialog>
    </form>
  )
}
