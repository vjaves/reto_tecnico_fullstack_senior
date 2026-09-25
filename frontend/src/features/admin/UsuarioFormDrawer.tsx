import { useEffect, useState, type FormEvent } from 'react'
import { toApiError } from '../../api/http'
import { adminApi } from '../../api/services'
import { useAuth } from '../../auth/AuthContext'
import { Alert, Button, Drawer, Field, Input, Segmented, Switch } from '../../components/ui'
import type { Rol, UsuarioAdmin } from '../../types'
import { sinClave } from '../../utils/format'

interface Props {
  open: boolean
  /** null = alta */
  usuario: UsuarioAdmin | null
  onClose: () => void
  onSaved: (u: UsuarioAdmin, esNuevo: boolean) => void
}

interface FormState {
  nombreUsuario: string
  nombreCompleto: string
  numeroDocumento: string
  email: string
  rol: Rol
  activo: boolean
  clave: string
}

const VACIO: FormState = { nombreUsuario: '', nombreCompleto: '', numeroDocumento: '', email: '', rol: 'User', activo: true, clave: '' }

/** Reglas de la política de claves del backend (ReglasClave.ClaveSegura). */
export const REGLAS_CLAVE: { texto: string; ok: (c: string) => boolean }[] = [
  { texto: '8 caracteres o más', ok: (c) => c.length >= 8 },
  { texto: 'Una mayúscula', ok: (c) => /[A-Z]/.test(c) },
  { texto: 'Una minúscula', ok: (c) => /[a-z]/.test(c) },
  { texto: 'Un número', ok: (c) => /[0-9]/.test(c) },
]

export function ChecklistClave({ clave }: { clave: string }) {
  return (
    <ul className="mt-2 grid grid-cols-2 gap-1 text-xs">
      {REGLAS_CLAVE.map((r) => {
        const ok = r.ok(clave)
        return (
          <li key={r.texto} className={ok ? 'text-emerald-700' : 'text-slate-400'}>
            <span aria-hidden="true">{ok ? '✓' : '○'}</span> {r.texto}
          </li>
        )
      })}
    </ul>
  )
}

/** Clave aleatoria que cumple la política, para entregar al usuario y que la cambie. */
export function generarClave(): string {
  const bloques = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789']
  const aleatorio = (s: string) => s[crypto.getRandomValues(new Uint32Array(1))[0] % s.length]
  const base = [aleatorio(bloques[0]), aleatorio(bloques[1]), aleatorio(bloques[2])]
  while (base.length < 12) base.push(aleatorio(bloques.join('')))
  return base.sort(() => crypto.getRandomValues(new Uint8Array(1))[0] - 128).join('')
}

function validar(f: FormState, esNuevo: boolean): Record<string, string> {
  const e: Record<string, string> = {}
  if (esNuevo && !/^[A-Za-z0-9._-]+$/.test(f.nombreUsuario.trim())) e.nombreUsuario = 'Letras, números, punto, guion y guion bajo.'
  if (!f.nombreCompleto.trim()) e.nombreCompleto = 'El nombre es obligatorio.'
  if (!f.numeroDocumento.trim()) e.numeroDocumento = 'El documento es obligatorio.'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = 'El email no es válido.'
  if (esNuevo && !REGLAS_CLAVE.every((r) => r.ok(f.clave))) e.clave = 'La contraseña no cumple la política.'
  return e
}

export function UsuarioFormDrawer({ open, usuario, onClose, onSaved }: Props) {
  const { usuario: sesion } = useAuth()
  const esNuevo = !usuario
  const esUnoMismo = usuario?.id === sesion?.id

  const [form, setForm] = useState<FormState>(VACIO)
  const [guardando, setGuardando] = useState(false)
  const [intento, setIntento] = useState(false)
  const [erroresServidor, setErroresServidor] = useState<Record<string, string>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [verClave, setVerClave] = useState(false)

  useEffect(() => {
    if (!open) return
    setIntento(false)
    setErroresServidor({})
    setErrorGeneral(null)
    setForm(
      usuario
        ? { ...VACIO, nombreUsuario: usuario.nombreUsuario, nombreCompleto: usuario.nombreCompleto, numeroDocumento: usuario.numeroDocumento, email: usuario.email, rol: usuario.rol, activo: usuario.activo }
        : VACIO,
    )
  }, [open, usuario])

  const errores = { ...(intento ? validar(form, esNuevo) : {}), ...erroresServidor }

  function set<K extends keyof FormState>(campo: K, valor: FormState[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    setErroresServidor((e) => sinClave(e, campo))
  }

  async function guardar(e?: FormEvent) {
    e?.preventDefault()
    setIntento(true)
    setErrorGeneral(null)
    if (Object.keys(validar(form, esNuevo)).length > 0) return

    setGuardando(true)
    try {
      const guardado = usuario
        ? await adminApi.actualizarUsuario(usuario.id, {
            nombreCompleto: form.nombreCompleto,
            numeroDocumento: form.numeroDocumento,
            email: form.email,
            rol: form.rol,
            activo: form.activo,
          })
        : await adminApi.crearUsuario({
            nombreUsuario: form.nombreUsuario,
            nombreCompleto: form.nombreCompleto,
            numeroDocumento: form.numeroDocumento,
            email: form.email,
            rol: form.rol,
            clave: form.clave,
          })
      onSaved(guardado, esNuevo)
    } catch (err) {
      const apiError = toApiError(err)
      const campos = Object.fromEntries(Object.entries(apiError.fieldErrors).map(([k, v]) => [k, v[0]]))
      if (apiError.code === 'USUARIO_EMAIL_DUPLICADO') campos.email = apiError.message
      if (apiError.code === 'USUARIO_NOMBRE_DUPLICADO') campos.nombreUsuario = apiError.message
      setErroresServidor(campos)
      if (Object.keys(campos).length === 0) setErrorGeneral(apiError.message)
    } finally {
      setGuardando(false)
    }
  }

  const campo = (clave: keyof FormState, label: string, props: { type?: string; max?: number; disabled?: boolean; hint?: string } = {}) => (
    <Field label={label} required error={errores[clave]} hint={props.hint}>
      {(id, desc) => (
        <Input
          id={id}
          aria-describedby={desc}
          type={props.type ?? 'text'}
          value={form[clave] as string}
          maxLength={props.max}
          disabled={props.disabled}
          invalid={!!errores[clave]}
          onChange={(e) => set(clave, e.target.value)}
        />
      )}
    </Field>
  )

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={esNuevo ? 'Nuevo usuario' : 'Editar usuario'}
      subtitle={esNuevo ? 'El usuario ingresará con su email y la contraseña inicial.' : `@${usuario?.nombreUsuario}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={guardando}>
            Cancelar
          </Button>
          <Button type="submit" form="form-usuario" loading={guardando}>
            {esNuevo ? 'Crear usuario' : 'Guardar cambios'}
          </Button>
        </>
      }
    >
      <form id="form-usuario" onSubmit={guardar} noValidate className="space-y-5">
        {errorGeneral && <Alert>{errorGeneral}</Alert>}
        {!esNuevo && (usuario?.rol !== form.rol || usuario?.activo !== form.activo) && (
          <Alert tone="warning">Al guardar, las sesiones abiertas de este usuario se cerrarán: deberá volver a iniciar sesión.</Alert>
        )}

        {esNuevo && campo('nombreUsuario', 'Usuario', { max: 50, hint: 'Identificador interno, sin espacios.' })}
        {campo('nombreCompleto', 'Nombre completo', { max: 300 })}
        <div className="grid gap-4 sm:grid-cols-2">
          {campo('numeroDocumento', 'Documento', { max: 20 })}
          {campo('email', 'Email (usuario de ingreso)', { type: 'email', max: 300 })}
        </div>

        <div>
          <p className="mb-1.5 text-sm font-medium text-slate-700">Rol</p>
          {esUnoMismo ? (
            <p className="text-sm text-slate-500">No puedes cambiar tu propio rol.</p>
          ) : (
            <Segmented
              label="Rol"
              value={form.rol}
              onChange={(v) => set('rol', v)}
              opciones={[
                { value: 'User', label: 'User — opera pedidos' },
                { value: 'Admin', label: 'Admin — también elimina y administra' },
              ]}
            />
          )}
        </div>

        {esNuevo ? (
          <Field label="Contraseña inicial" required error={errores.clave}>
            {(id, desc) => (
              <>
                <div className="flex gap-2">
                  <Input
                    id={id}
                    aria-describedby={desc}
                    type={verClave ? 'text' : 'password'}
                    autoComplete="new-password"
                    value={form.clave}
                    maxLength={72}
                    invalid={!!errores.clave}
                    onChange={(e) => set('clave', e.target.value)}
                    className="font-mono"
                  />
                  <Button
                    variant="secondary"
                    onClick={() => {
                      set('clave', generarClave())
                      setVerClave(true)
                    }}
                  >
                    Generar
                  </Button>
                </div>
                <ChecklistClave clave={form.clave} />
              </>
            )}
          </Field>
        ) : (
          <Switch
            checked={form.activo}
            onChange={(v) => set('activo', v)}
            disabled={esUnoMismo}
            label="Acceso habilitado"
            description={esUnoMismo ? 'No puedes deshabilitar tu propia cuenta.' : 'Si se deshabilita, no podrá ingresar y su sesión actual se cierra.'}
          />
        )}
      </form>
    </Drawer>
  )
}
