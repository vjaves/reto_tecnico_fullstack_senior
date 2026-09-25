import { useEffect, useState, type FormEvent } from 'react'
import { toApiError } from '../../api/http'
import { catalogosApi, productosApi } from '../../api/services'
import { Alert, Button, Drawer, Field, Input, Select, Spinner, Switch, Textarea } from '../../components/ui'
import type { GuardarProductoRequest, ItemCatalogo, ProductoDetalle } from '../../types'
import { sinClave } from '../../utils/format'

interface Props {
  open: boolean
  /** null = alta */
  productoId: number | null
  onClose: () => void
  onSaved: (producto: ProductoDetalle, esNuevo: boolean) => void
}

const VACIO: GuardarProductoRequest = { codigo: '', nombre: '', descripcion: '', unidadMedidaId: 0, activo: true }

function validar(f: GuardarProductoRequest): Record<string, string> {
  const e: Record<string, string> = {}
  if (!f.codigo.trim()) e.codigo = 'El código es obligatorio.'
  else if (!/^[A-Za-z0-9._-]+$/.test(f.codigo.trim())) e.codigo = 'Sólo letras, números, punto, guion y guion bajo.'
  if (!f.nombre.trim()) e.nombre = 'El nombre es obligatorio.'
  if (!f.unidadMedidaId) e.unidadMedidaId = 'Seleccione la unidad de medida.'
  return e
}

export function ProductoFormDrawer({ open, productoId, onClose, onSaved }: Props) {
  const [unidades, setUnidades] = useState<ItemCatalogo[]>([])
  const [form, setForm] = useState<GuardarProductoRequest>(VACIO)
  const [cargando, setCargando] = useState(false)
  const [guardando, setGuardando] = useState(false)
  const [intento, setIntento] = useState(false)
  const [erroresServidor, setErroresServidor] = useState<Record<string, string>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    let vigente = true
    setCargando(true)
    setIntento(false)
    setErroresServidor({})
    setErrorGeneral(null)
    Promise.all([catalogosApi.unidadesMedida(), productoId ? productosApi.obtener(productoId) : Promise.resolve(null)])
      .then(([u, p]) => {
        if (!vigente) return
        setUnidades(u)
        setForm(
          p
            ? { codigo: p.codigo ?? '', nombre: p.nombre, descripcion: p.descripcion ?? '', unidadMedidaId: p.unidadMedidaId ?? 0, activo: p.activo }
            : { ...VACIO, unidadMedidaId: u.find((x) => x.abreviatura === 'UND')?.id ?? u[0]?.id ?? 0 },
        )
      })
      .catch((e) => vigente && setErrorGeneral(toApiError(e).message))
      .finally(() => vigente && setCargando(false))
    return () => {
      vigente = false
    }
  }, [open, productoId])

  const errores = { ...(intento ? validar(form) : {}), ...erroresServidor }

  function set<K extends keyof GuardarProductoRequest>(campo: K, valor: GuardarProductoRequest[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    setErroresServidor((e) => sinClave(e, campo))
  }

  async function guardar(e?: FormEvent) {
    e?.preventDefault()
    setIntento(true)
    setErrorGeneral(null)
    if (Object.keys(validar(form)).length > 0) return

    setGuardando(true)
    try {
      const guardado = productoId ? await productosApi.actualizar(productoId, form) : await productosApi.crear(form)
      onSaved(guardado, !productoId)
    } catch (err) {
      const apiError = toApiError(err)
      const campos = Object.fromEntries(Object.entries(apiError.fieldErrors).map(([k, v]) => [k, v[0]]))
      if (apiError.code === 'PRODUCTO_CODIGO_DUPLICADO') campos.codigo = apiError.message
      setErroresServidor(campos)
      if (Object.keys(campos).length === 0) setErrorGeneral(apiError.message)
    } finally {
      setGuardando(false)
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={productoId ? 'Editar producto' : 'Nuevo producto'}
      subtitle="El precio se define en cada línea del pedido."
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={guardando}>
            Cancelar
          </Button>
          <Button type="submit" form="form-producto" loading={guardando} disabled={cargando}>
            {productoId ? 'Guardar cambios' : 'Registrar producto'}
          </Button>
        </>
      }
    >
      {cargando ? (
        <div className="grid place-items-center py-20 text-brand-600">
          <Spinner className="size-7" />
        </div>
      ) : (
        <form id="form-producto" onSubmit={guardar} noValidate className="space-y-5">
          {errorGeneral && <Alert>{errorGeneral}</Alert>}

          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Código" required error={errores.codigo} hint="Único en la empresa.">
              {(id, desc) => (
                <Input
                  id={id}
                  aria-describedby={desc}
                  value={form.codigo}
                  maxLength={50}
                  invalid={!!errores.codigo}
                  onChange={(e) => set('codigo', e.target.value.toUpperCase())}
                  className="font-mono"
                  autoFocus
                />
              )}
            </Field>
            <Field label="Unidad de medida" required error={errores.unidadMedidaId} className="sm:col-span-2">
              {(id, desc) => (
                <Select id={id} aria-describedby={desc} value={form.unidadMedidaId} invalid={!!errores.unidadMedidaId} onChange={(e) => set('unidadMedidaId', Number(e.target.value))}>
                  <option value={0}>Seleccione…</option>
                  {unidades.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.nombre} {u.abreviatura ? `(${u.abreviatura})` : ''}
                    </option>
                  ))}
                </Select>
              )}
            </Field>
          </div>

          <Field label="Nombre" required error={errores.nombre}>
            {(id, desc) => (
              <Input id={id} aria-describedby={desc} value={form.nombre} maxLength={300} invalid={!!errores.nombre} onChange={(e) => set('nombre', e.target.value)} />
            )}
          </Field>

          <Field label="Descripción" error={errores.descripcion} hint={`${form.descripcion.length}/2000`}>
            {(id, desc) => (
              <Textarea id={id} aria-describedby={desc} value={form.descripcion} maxLength={2000} rows={4} onChange={(e) => set('descripcion', e.target.value)} />
            )}
          </Field>

          <Switch
            checked={form.activo}
            onChange={(v) => set('activo', v)}
            label="Producto activo"
            description="Los productos inactivos no se ofrecen en pedidos nuevos."
          />
        </form>
      )}
    </Drawer>
  )
}
