import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'

type Tono = 'success' | 'error' | 'info'

interface Toast {
  id: number
  tono: Tono
  mensaje: string
}

interface ToastContextValue {
  notify: (mensaje: string, tono?: Tono) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

const ESTILO: Record<Tono, string> = {
  success: 'border-emerald-500',
  error: 'border-red-500',
  info: 'border-brand-500',
}

const ICONO: Record<Tono, string> = { success: '✓', error: '!', info: 'i' }

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])

  const cerrar = useCallback((id: number) => setToasts((t) => t.filter((x) => x.id !== id)), [])

  const notify = useCallback(
    (mensaje: string, tono: Tono = 'success') => {
      const id = Date.now() + Math.random()
      setToasts((t) => [...t.slice(-3), { id, tono, mensaje }])
      window.setTimeout(() => cerrar(id), tono === 'error' ? 7000 : 4000)
    },
    [cerrar],
  )

  const value = useMemo(() => ({ notify }), [notify])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div aria-live="polite" className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4 sm:items-end">
        {toasts.map((t) => (
          <div
            key={t.id}
            role={t.tono === 'error' ? 'alert' : 'status'}
            className={`animate-toast-in pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border-l-4 bg-white px-4 py-3 text-sm shadow-lg ring-1 ring-slate-200 ${ESTILO[t.tono]}`}
          >
            <span className="mt-px font-bold text-slate-500" aria-hidden="true">
              {ICONO[t.tono]}
            </span>
            <p className="flex-1 text-slate-700">{t.mensaje}</p>
            <button onClick={() => cerrar(t.id)} className="text-slate-400 hover:text-slate-600" aria-label="Cerrar notificación">
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast debe usarse dentro de ToastProvider')
  return ctx
}
