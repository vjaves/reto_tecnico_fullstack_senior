import { useCallback, useEffect, useState } from 'react'
import { toApiError } from '../../api/http'
import { adminApi } from '../../api/services'
import { Alert, Button, Card, Spinner, Switch, cx } from '../../components/ui'
import type { EstadoCircuito, EstadoSalud, EstadoSistema } from '../../types'

const SALUD: Record<EstadoSalud, { texto: string; clase: string; punto: string }> = {
  Healthy: { texto: 'Operativo', clase: 'text-emerald-700 bg-emerald-50 ring-emerald-200', punto: 'bg-emerald-500' },
  Degraded: { texto: 'Degradado', clase: 'text-accent-700 bg-accent-50 ring-accent-200', punto: 'bg-accent-500' },
  Unhealthy: { texto: 'Caído', clase: 'text-red-700 bg-red-50 ring-red-200', punto: 'bg-red-500' },
}

const CIRCUITO: Record<EstadoCircuito, { texto: string; detalle: string; salud: EstadoSalud }> = {
  Closed: { texto: 'Cerrado', detalle: 'Las consultas llegan a SQL Server normalmente.', salud: 'Healthy' },
  HalfOpen: { texto: 'Semiabierto', detalle: 'Probando si SQL Server se recuperó con peticiones de prueba.', salud: 'Degraded' },
  Open: { texto: 'Abierto', detalle: 'SQL Server falló repetidamente: se responde 503 sin tocar la BD hasta que pase el tiempo de apertura.', salud: 'Unhealthy' },
  Isolated: { texto: 'Aislado', detalle: 'Abierto manualmente.', salud: 'Unhealthy' },
}

const NOMBRE_CHEQUEO: Record<string, string> = { sqlserver: 'SQL Server', 'circuito-sql': 'Circuit breaker SQL' }

function uptime(segundos: number) {
  const d = Math.floor(segundos / 86400)
  const h = Math.floor((segundos % 86400) / 3600)
  const m = Math.floor((segundos % 3600) / 60)
  return [d && `${d} d`, (d || h) && `${h} h`, `${m} min`].filter(Boolean).join(' ')
}

function Indicador({ titulo, valor, detalle, salud }: { titulo: string; valor: string; detalle?: string; salud: EstadoSalud }) {
  const s = SALUD[salud]
  return (
    <Card className="p-5">
      <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">{titulo}</p>
      <div className="mt-2 flex items-center gap-2">
        <span className={cx('size-2.5 rounded-full', s.punto, salud !== 'Healthy' && 'animate-pulse')} aria-hidden="true" />
        <span className="text-xl font-bold text-slate-900">{valor}</span>
      </div>
      {detalle && <p className="mt-1.5 text-xs text-slate-500">{detalle}</p>}
    </Card>
  )
}

export function SistemaTab() {
  const [estado, setEstado] = useState<EstadoSistema | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cargando, setCargando] = useState(false)
  const [auto, setAuto] = useState(true)
  const [actualizado, setActualizado] = useState<Date | null>(null)

  const cargar = useCallback(async (signal?: AbortSignal) => {
    setCargando(true)
    try {
      setEstado(await adminApi.sistema(signal))
      setError(null)
      setActualizado(new Date())
    } catch (e) {
      if (!signal?.aborted) setError(toApiError(e).message)
    } finally {
      if (!signal?.aborted) setCargando(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void cargar(controller.signal)
    if (!auto) return () => controller.abort()
    const id = window.setInterval(() => void cargar(controller.signal), 10_000)
    return () => {
      controller.abort()
      window.clearInterval(id)
    }
  }, [auto, cargar])

  if (!estado && !error)
    return (
      <div className="grid place-items-center py-20 text-brand-600">
        <Spinner className="size-7" />
      </div>
    )

  const circuito = estado ? CIRCUITO[estado.circuitoSql] : null
  const bd = estado?.chequeos.find((c) => c.nombre === 'sqlserver')

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-500">{actualizado ? `Actualizado ${actualizado.toLocaleTimeString('es-PE')}` : ''}</p>
        <div className="flex items-center gap-3">
          <div className="w-64">
            <Switch checked={auto} onChange={setAuto} label="Actualizar cada 10 s" />
          </div>
          <Button variant="secondary" loading={cargando} onClick={() => void cargar()}>
            Actualizar
          </Button>
        </div>
      </div>

      {error && <Alert>No se pudo obtener el estado: {error}</Alert>}

      {estado && circuito && (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Indicador titulo="Estado general" valor={SALUD[estado.estado].texto} detalle={`${estado.entorno} · v${estado.version}`} salud={estado.estado} />
            <Indicador titulo="Base de datos" valor={bd ? SALUD[bd.estado].texto : '—'} detalle={bd ? `Respuesta en ${bd.duracionMs} ms` : undefined} salud={bd?.estado ?? 'Unhealthy'} />
            <Indicador titulo="Circuit breaker SQL" valor={circuito.texto} detalle={circuito.detalle} salud={circuito.salud} />
            <Indicador titulo="Tiempo activo" valor={uptime(estado.segundosActivo)} detalle={`Desde ${new Date(estado.inicioUtc).toLocaleString('es-PE')}`} salud="Healthy" />
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <h3 className="flex items-center gap-2 border-b border-slate-200 px-5 py-4 font-semibold text-slate-900">
                <span className="h-4 w-1 rounded-full bg-accent-600" aria-hidden="true" />
                Chequeos de salud
              </h3>
              <ul className="divide-y divide-slate-100">
                {estado.chequeos.map((c) => (
                  <li key={c.nombre} className="flex items-start justify-between gap-4 px-5 py-3 text-sm">
                    <div>
                      <p className="font-medium text-slate-800">{NOMBRE_CHEQUEO[c.nombre] ?? c.nombre}</p>
                      {c.descripcion && <p className="text-xs text-slate-500">{c.descripcion}</p>}
                    </div>
                    <span className={cx('shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset', SALUD[c.estado].clase)}>
                      {SALUD[c.estado].texto} · {c.duracionMs} ms
                    </span>
                  </li>
                ))}
              </ul>
            </Card>

            <Card>
              <h3 className="flex items-center gap-2 border-b border-slate-200 px-5 py-4 font-semibold text-slate-900">
                <span className="h-4 w-1 rounded-full bg-brand-600" aria-hidden="true" />
                Seguridad y resiliencia vigentes
              </h3>
              <dl className="divide-y divide-slate-100">
                {Object.entries(estado.configuracion).map(([k, v]) => (
                  <div key={k} className="flex justify-between gap-4 px-5 py-2.5 text-sm">
                    <dt className="text-slate-500">{k}</dt>
                    <dd className="text-right font-medium text-slate-800">{v}</dd>
                  </div>
                ))}
              </dl>
            </Card>
          </div>
        </>
      )}
    </div>
  )
}
