import { useEffect, useState, type FormEvent } from 'react'
import { toApiError } from '../../api/http'
import { catalogosApi, clientesApi } from '../../api/services'
import { Alert, Button, Drawer, Field, Input, Segmented, Spinner, Switch } from '../../components/ui'
import type { ClienteDetalle, GuardarClienteRequest, ItemCatalogo } from '../../types'
import { sinClave } from '../../utils/format'

interface Props {
  open: boolean
  /** null = alta */
  clienteId: number | null
  onClose: () => void
  onSaved: (cliente: ClienteDetalle, esNuevo: boolean) => void
}

const VACIO: GuardarClienteRequest = {
  tipoDocumentoId: 0,
  numeroDocumento: '',
  primerNombre: '',
  segundoNombre: '',
  apellidoPaterno: '',
  apellidoMaterno: '',
  razonSocial: '',
  nombreComercial: '',
  direccion: '',
  celular: '',
  activo: true,
}

const LARGO_DOCUMENTO: Record<string, number> = { DNI: 8, RUC: 11 }

/** Mismas reglas que Cliente.ValidarDocumento del dominio, para avisar antes de enviar. */
function validar(f: GuardarClienteRequest, abreviatura: string | undefined): Record<string, string> {
  const e: Record<string, string> = {}
  const doc = f.numeroDocumento.trim()
  const esRuc = abreviatura === 'RUC'
  if (!f.tipoDocumentoId) e.tipoDocumentoId = 'Seleccione el tipo de documento.'
  if (!doc) e.numeroDocumento = 'El número de documento es obligatorio.'
  else if (abreviatura === 'DNI' && !/^\d{8}$/.test(doc)) e.numeroDocumento = 'El DNI debe tener 8 dígitos.'
  else if (esRuc && !/^\d{11}$/.test(doc)) e.numeroDocumento = 'El RUC debe tener 11 dígitos.'
  else if (esRuc && !/^(10|15|17|20)/.test(doc)) e.numeroDocumento = 'El RUC debe empezar con 10, 15, 17 o 20.'
  if (esRuc && !f.razonSocial.trim()) e.razonSocial = 'La razón social es obligatoria para un RUC.'
  if (!esRuc && !f.primerNombre.trim()) e.primerNombre = 'El nombre es obligatorio.'
  if (!esRuc && !f.apellidoPaterno.trim()) e.apellidoPaterno = 'El apellido paterno es obligatorio.'
  if (f.celular && !/^[0-9+\-\s]*$/.test(f.celular)) e.celular = 'Sólo números, espacios, + y -.'
  return e
}

export function ClienteFormDrawer({ open, clienteId, onClose, onSaved }: Props) {
  const [tipos, setTipos] = useState<ItemCatalogo[]>([])
  const [form, setForm] = useState<GuardarClienteRequest>(VACIO)
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
    Promise.all([catalogosApi.tiposDocumento(), clienteId ? clientesApi.obtener(clienteId) : Promise.resolve(null)])
      .then(([t, c]) => {
        if (!vigente) return
        setTipos(t)
        setForm(
          c
            ? {
                tipoDocumentoId: c.tipoDocumentoId,
                numeroDocumento: c.numeroDocumento,
                primerNombre: c.primerNombre ?? '',
                segundoNombre: c.segundoNombre ?? '',
                apellidoPaterno: c.apellidoPaterno ?? '',
                apellidoMaterno: c.apellidoMaterno ?? '',
                razonSocial: c.razonSocial ?? '',
                nombreComercial: c.nombreComercial ?? '',
                direccion: c.direccion ?? '',
                celular: c.celular ?? '',
                activo: c.activo,
              }
            : { ...VACIO, tipoDocumentoId: t[0]?.id ?? 0 },
        )
      })
      .catch((e) => vigente && setErrorGeneral(toApiError(e).message))
      .finally(() => vigente && setCargando(false))
    return () => {
      vigente = false
    }
  }, [open, clienteId])

  const abreviatura = tipos.find((t) => t.id === form.tipoDocumentoId)?.abreviatura ?? undefined
  const esRuc = abreviatura === 'RUC'
  const largo = abreviatura ? LARGO_DOCUMENTO[abreviatura] : undefined
  const errores = { ...(intento ? validar(form, abreviatura) : {}), ...erroresServidor }

  function set<K extends keyof GuardarClienteRequest>(campo: K, valor: GuardarClienteRequest[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    setErroresServidor((e) => sinClave(e, campo))
  }

  async function guardar(e?: FormEvent) {
    e?.preventDefault()
    setIntento(true)
    setErrorGeneral(null)
    if (Object.keys(validar(form, abreviatura)).length > 0) return

    setGuardando(true)
    try {
      const guardado = clienteId ? await clientesApi.actualizar(clienteId, form) : await clientesApi.crear(form)
      onSaved(guardado, !clienteId)
    } catch (err) {
      const apiError = toApiError(err)
      const campos = Object.fromEntries(Object.entries(apiError.fieldErrors).map(([k, v]) => [k, v[0]]))
      if (apiError.code === 'CLIENTE_DOCUMENTO_DUPLICADO') campos.numeroDocumento = apiError.message
      setErroresServidor(campos)
      if (Object.keys(campos).length === 0) setErrorGeneral(apiError.message)
    } finally {
      setGuardando(false)
    }
  }

  const texto = (campo: keyof GuardarClienteRequest, label: string, opciones: { required?: boolean; max?: number; placeholder?: string; className?: string } = {}) => (
    <Field label={label} required={opciones.required} error={errores[campo]} className={opciones.className}>
      {(id, desc) => (
        <Input
          id={id}
          aria-describedby={desc}
          value={form[campo] as string}
          maxLength={opciones.max}
          placeholder={opciones.placeholder}
          invalid={!!errores[campo]}
          onChange={(e) => set(campo, e.target.value)}
        />
      )}
    </Field>
  )

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={clienteId ? 'Editar cliente' : 'Nuevo cliente'}
      subtitle={clienteId ? 'Los cambios se reflejan en los pedidos nuevos.' : 'Persona natural (DNI) o empresa (RUC).'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={guardando}>
            Cancelar
          </Button>
          <Button type="submit" form="form-cliente" loading={guardando} disabled={cargando}>
            {clienteId ? 'Guardar cambios' : 'Registrar cliente'}
          </Button>
        </>
      }
    >
      {cargando ? (
        <div className="grid place-items-center py-20 text-brand-600">
          <Spinner className="size-7" />
        </div>
      ) : (
        <form id="form-cliente" onSubmit={guardar} noValidate className="space-y-5">
          {errorGeneral && <Alert>{errorGeneral}</Alert>}

          <div>
            <p className="mb-1.5 text-sm font-medium text-slate-700">Tipo de documento</p>
            <Segmented
              label="Tipo de documento"
              value={String(form.tipoDocumentoId)}
              onChange={(v) => set('tipoDocumentoId', Number(v))}
              opciones={tipos.map((t) => ({ value: String(t.id), label: t.abreviatura ?? t.nombre }))}
            />
          </div>

          <Field label={abreviatura ? `Número de ${abreviatura}` : 'Número de documento'} required error={errores.numeroDocumento} hint={largo ? `${largo} dígitos` : undefined}>
            {(id, desc) => (
              <Input
                id={id}
                aria-describedby={desc}
                value={form.numeroDocumento}
                inputMode={largo ? 'numeric' : 'text'}
                maxLength={largo ?? 20}
                invalid={!!errores.numeroDocumento}
                onChange={(e) => set('numeroDocumento', largo ? e.target.value.replace(/\D/g, '') : e.target.value)}
                className="font-mono tracking-wide"
                autoFocus
              />
            )}
          </Field>

          {esRuc ? (
            <div className="animate-fade-in space-y-5">
              {texto('razonSocial', 'Razón social', { required: true, max: 200, placeholder: 'EMPRESA S.A.C.' })}
              {texto('nombreComercial', 'Nombre comercial', { max: 100 })}
            </div>
          ) : (
            <div className="animate-fade-in grid gap-4 sm:grid-cols-2">
              {texto('primerNombre', 'Primer nombre', { required: true, max: 100 })}
              {texto('segundoNombre', 'Segundo nombre', { max: 100 })}
              {texto('apellidoPaterno', 'Apellido paterno', { required: true, max: 100 })}
              {texto('apellidoMaterno', 'Apellido materno', { max: 100 })}
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-3">
            {texto('direccion', 'Dirección', { max: 200, className: 'sm:col-span-2' })}
            {texto('celular', 'Celular', { max: 20, placeholder: '987 654 321' })}
          </div>

          <Switch
            checked={form.activo}
            onChange={(v) => set('activo', v)}
            label="Cliente activo"
            description="Los clientes inactivos no aparecen al registrar pedidos."
          />
        </form>
      )}
    </Drawer>
  )
}
