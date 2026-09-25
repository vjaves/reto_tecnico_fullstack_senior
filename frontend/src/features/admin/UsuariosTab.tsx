import { useCallback, useEffect, useState } from 'react'
import { toApiError } from '../../api/http'
import { adminApi } from '../../api/services'
import { useAuth } from '../../auth/AuthContext'
import { IconLock, IconPencil, IconPlus, IconSearch } from '../../components/icons'
import { useToast } from '../../components/Toast'
import { ActivoBadge, Alert, Button, Card, ConfirmDialog, EmptyState, Input, SkeletonRows, cx } from '../../components/ui'
import { useDebounce } from '../../hooks/useDebounce'
import type { UsuarioAdmin } from '../../types'
import { ChecklistClave, REGLAS_CLAVE, UsuarioFormDrawer, generarClave } from './UsuarioFormDrawer'

const fechaHora = (iso: string | null) => (iso ? new Date(iso).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' }) : 'Nunca')

export function UsuariosTab() {
  const { usuario: sesion } = useAuth()
  const { notify } = useToast()
  const [buscar, setBuscar] = useState('')
  const buscarDebounced = useDebounce(buscar)
  const [usuarios, setUsuarios] = useState<UsuarioAdmin[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)

  const [editando, setEditando] = useState<{ usuario: UsuarioAdmin | null } | null>(null)
  const [aRestablecer, setARestablecer] = useState<UsuarioAdmin | null>(null)
  const [nuevaClave, setNuevaClave] = useState('')
  const [procesando, setProcesando] = useState(false)

  const recargar = useCallback(() => setVersion((v) => v + 1), [])

  useEffect(() => {
    let vigente = true
    adminApi
      .usuarios(buscarDebounced)
      .then((u) => vigente && (setUsuarios(u), setError(null)))
      .catch((e) => vigente && setError(toApiError(e).message))
    return () => {
      vigente = false
    }
  }, [buscarDebounced, version])

  async function desbloquear(u: UsuarioAdmin) {
    try {
      await adminApi.desbloquear(u.id)
      notify(`${u.nombreCompleto} fue desbloqueado.`)
      recargar()
    } catch (e) {
      notify(toApiError(e).message, 'error')
    }
  }

  async function restablecer() {
    if (!aRestablecer || !REGLAS_CLAVE.every((r) => r.ok(nuevaClave))) return
    setProcesando(true)
    try {
      await adminApi.restablecerClave(aRestablecer.id, nuevaClave)
      notify(`Contraseña de ${aRestablecer.nombreCompleto} restablecida. Sus sesiones abiertas se cerraron.`)
      setARestablecer(null)
      recargar()
    } catch (e) {
      notify(toApiError(e).message, 'error')
    } finally {
      setProcesando(false)
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-60 flex-1">
          <IconSearch className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
          <Input aria-label="Buscar usuarios" placeholder="Buscar por nombre, email o usuario…" value={buscar} onChange={(e) => setBuscar(e.target.value)} className="pl-9" />
        </div>
        <Button variant="accent" icon={<IconPlus className="size-4" />} onClick={() => setEditando({ usuario: null })}>
          Nuevo usuario
        </Button>
      </div>

      {error && <Alert>{error}</Alert>}

      <Card className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50/80 text-left text-xs font-semibold tracking-wide text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-3">Usuario</th>
                <th className="px-4 py-3">Rol</th>
                <th className="px-4 py-3">Acceso</th>
                <th className="hidden px-4 py-3 md:table-cell">Último ingreso</th>
                <th className="px-4 py-3">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {!usuarios && <SkeletonRows columnas={5} filas={3} />}
              {usuarios?.map((u) => (
                <tr key={u.id} className="hover:bg-brand-50/40">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <span
                        className={cx('grid size-9 shrink-0 place-items-center rounded-full text-xs font-bold', u.rol === 'Admin' ? 'bg-accent-600 text-white' : 'bg-brand-100 text-brand-700')}
                        aria-hidden="true"
                      >
                        {u.nombreCompleto
                          .split(' ')
                          .slice(0, 2)
                          .map((p) => p[0])
                          .join('')}
                      </span>
                      <div className="min-w-0 leading-tight">
                        <p className="font-medium text-slate-900">
                          {u.nombreCompleto}
                          {u.id === sesion?.id && <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-500">TÚ</span>}
                        </p>
                        <p className="truncate text-xs text-slate-500">{u.email}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={cx(
                        'rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset',
                        u.rol === 'Admin' ? 'bg-accent-50 text-accent-700 ring-accent-200' : 'bg-brand-50 text-brand-700 ring-brand-200',
                      )}
                    >
                      {u.rol}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <ActivoBadge activo={u.activo} />
                      {u.bloqueado && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-semibold text-red-700 ring-1 ring-red-200 ring-inset">
                          <IconLock className="size-3" /> Bloqueado
                        </span>
                      )}
                      {!u.bloqueado && u.intentosFallidos > 0 && <span className="text-xs text-slate-400">{u.intentosFallidos} intento(s) fallido(s)</span>}
                    </div>
                  </td>
                  <td className="hidden px-4 py-3 text-slate-600 md:table-cell">{fechaHora(u.fechaUltimoLogin)}</td>
                  <td className="px-4 py-3 text-right whitespace-nowrap">
                    {u.bloqueado && (
                      <Button size="sm" variant="secondary" onClick={() => desbloquear(u)} className="mr-1">
                        Desbloquear
                      </Button>
                    )}
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => {
                        setNuevaClave(generarClave())
                        setARestablecer(u)
                      }}
                    >
                      Restablecer clave
                    </Button>
                    <button
                      onClick={() => setEditando({ usuario: u })}
                      className="ml-1 inline-flex rounded-lg p-2 text-slate-500 hover:bg-brand-50 hover:text-brand-700"
                      aria-label={`Editar ${u.nombreCompleto}`}
                      title="Editar"
                    >
                      <IconPencil className="size-4" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {usuarios && usuarios.length === 0 && <EmptyState titulo="Sin resultados" descripcion="Ningún usuario coincide con la búsqueda." />}
      </Card>

      <UsuarioFormDrawer
        open={!!editando}
        usuario={editando?.usuario ?? null}
        onClose={() => setEditando(null)}
        onSaved={(u, esNuevo) => {
          notify(esNuevo ? `Usuario ${u.email} creado.` : `Usuario ${u.email} actualizado.`)
          setEditando(null)
          recargar()
        }}
      />

      <ConfirmDialog
        open={!!aRestablecer}
        title={`Restablecer la contraseña de ${aRestablecer?.nombreCompleto}`}
        confirmLabel="Restablecer"
        variant="primary"
        loading={procesando}
        onConfirm={restablecer}
        onCancel={() => setARestablecer(null)}
      >
        <p>Se asignará esta contraseña y se cerrarán las sesiones abiertas del usuario. Si estaba bloqueado, se desbloquea.</p>
        <div className="mt-3 flex gap-2">
          <Input aria-label="Nueva contraseña" value={nuevaClave} onChange={(e) => setNuevaClave(e.target.value)} className="font-mono" maxLength={72} />
          <Button variant="secondary" onClick={() => setNuevaClave(generarClave())}>
            Generar
          </Button>
          <Button
            variant="secondary"
            onClick={() => {
              void navigator.clipboard?.writeText(nuevaClave)
              notify('Contraseña copiada al portapapeles.', 'info')
            }}
          >
            Copiar
          </Button>
        </div>
        <ChecklistClave clave={nuevaClave} />
      </ConfirmDialog>
    </div>
  )
}
