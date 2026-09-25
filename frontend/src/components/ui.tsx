import { useEffect, useId, useRef, type ButtonHTMLAttributes, type ComponentProps, type ReactNode, type SelectHTMLAttributes } from 'react'
import type { EstadoPedido } from '../types'

const cx = (...clases: (string | false | null | undefined)[]) => clases.filter(Boolean).join(' ')

type Variante = 'primary' | 'accent' | 'secondary' | 'danger' | 'ghost'

// primary = azul corporativo (#0029E0) para guardar/confirmar; accent = naranja (#E04300) para crear.
const VARIANTES: Record<Variante, string> = {
  primary: 'bg-brand-600 text-white shadow-sm shadow-brand-600/20 hover:bg-brand-700 focus-visible:outline-brand-600 disabled:bg-brand-600/50',
  accent: 'bg-accent-600 text-white shadow-sm shadow-accent-600/25 hover:bg-accent-700 focus-visible:outline-accent-600 disabled:bg-accent-600/50',
  secondary: 'bg-white text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 hover:ring-slate-400 disabled:text-slate-400',
  danger: 'bg-red-600 text-white shadow-sm hover:bg-red-700 focus-visible:outline-red-600 disabled:bg-red-600/50',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 disabled:text-slate-300',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variante
  loading?: boolean
  icon?: ReactNode
  size?: 'sm' | 'md'
}

export function Button({ variant = 'primary', loading, icon, size = 'md', className, children, disabled, ...rest }: ButtonProps) {
  return (
    <button
      type="button"
      {...rest}
      disabled={disabled || loading}
      className={cx(
        'inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-all',
        'focus-visible:outline-2 focus-visible:outline-offset-2 active:translate-y-px disabled:cursor-not-allowed disabled:active:translate-y-0',
        size === 'sm' ? 'px-2.5 py-1.5 text-xs' : 'px-4 py-2.5 text-sm',
        VARIANTES[variant],
        className,
      )}
    >
      {loading ? <Spinner className="size-4" /> : icon}
      {children}
    </button>
  )
}

export function Spinner({ className = 'size-5' }: { className?: string }) {
  return (
    <svg className={cx('animate-spin', className)} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeOpacity="0.25" strokeWidth="4" />
      <path d="M22 12a10 10 0 0 1-10 10" stroke="currentColor" strokeWidth="4" strokeLinecap="round" />
    </svg>
  )
}

/** Isotipo: la "A" en blanco sobre azul y el punto naranja de la marca. */
export function Logo({ className = 'size-9' }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" className={className} aria-hidden="true">
      <rect width="32" height="32" rx="8" fill="#0029E0" />
      <path d="M9 23 16 8l7 15" fill="none" stroke="#fff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="16" cy="19.5" r="2.5" fill="#E04300" />
    </svg>
  )
}

interface FieldProps {
  label: string
  error?: string
  hint?: string
  required?: boolean
  children: (id: string, describedBy: string | undefined) => ReactNode
  className?: string
}

/** Etiqueta + control + mensaje de error, con los atributos ARIA enlazados. */
export function Field({ label, error, hint, required, children, className }: FieldProps) {
  const id = useId()
  const descId = error || hint ? `${id}-desc` : undefined
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1.5 block text-sm font-medium text-slate-700">
        {label}
        {required && (
          <span className="ml-0.5 text-accent-600" aria-hidden="true">
            *
          </span>
        )}
      </label>
      {children(id, descId)}
      {(error || hint) && (
        <p id={descId} className={cx('mt-1.5 text-xs', error ? 'font-medium text-red-600' : 'text-slate-500')}>
          {error ?? hint}
        </p>
      )}
    </div>
  )
}

const controlBase =
  'block w-full rounded-lg border-0 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm ring-1 ring-inset transition-shadow placeholder:text-slate-400 focus:ring-2 focus:ring-inset disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500'

const controlRing = (invalid?: boolean) =>
  invalid ? 'ring-red-400 focus:ring-red-500' : 'ring-slate-300 hover:ring-slate-400 focus:ring-brand-600'

export function Input({ invalid, className, ...rest }: ComponentProps<'input'> & { invalid?: boolean }) {
  return <input {...rest} aria-invalid={invalid || undefined} className={cx(controlBase, controlRing(invalid), className)} />
}

export function Textarea({ invalid, className, ...rest }: ComponentProps<'textarea'> & { invalid?: boolean }) {
  return <textarea {...rest} aria-invalid={invalid || undefined} className={cx(controlBase, controlRing(invalid), 'min-h-24', className)} />
}

export function Select({ invalid, className, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement> & { invalid?: boolean }) {
  return (
    <select {...rest} aria-invalid={invalid || undefined} className={cx(controlBase, 'pr-8', controlRing(invalid), className)}>
      {children}
    </select>
  )
}

/** Interruptor accesible (role="switch"). */
export function Switch({ checked, onChange, label, description, disabled }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string; disabled?: boolean }) {
  const id = useId()
  return (
    <div className="flex items-start justify-between gap-4 rounded-lg bg-slate-50 px-4 py-3 ring-1 ring-inset ring-slate-200">
      <div>
        <label htmlFor={id} className="text-sm font-medium text-slate-800">
          {label}
        </label>
        {description && <p className="text-xs text-slate-500">{description}</p>}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cx(
          'relative inline-flex h-6 w-11 shrink-0 rounded-full transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600 disabled:opacity-50',
          checked ? 'bg-brand-600' : 'bg-slate-300',
        )}
      >
        <span className={cx('absolute top-0.5 left-0.5 size-5 rounded-full bg-white shadow transition-transform', checked && 'translate-x-5')} />
      </button>
    </div>
  )
}

interface Opcion<T extends string> {
  value: T
  label: string
  count?: number
}

/** Grupo de botones exclusivos (filtros rápidos). */
export function Segmented<T extends string>({ value, onChange, opciones, label }: { value: T; onChange: (v: T) => void; opciones: Opcion<T>[]; label: string }) {
  return (
    <div role="group" aria-label={label} className="inline-flex rounded-lg bg-slate-100 p-1">
      {opciones.map((o) => (
        <button
          key={o.value || 'todos'}
          type="button"
          aria-pressed={value === o.value}
          onClick={() => onChange(o.value)}
          className={cx(
            'rounded-md px-3 py-1.5 text-xs font-semibold transition-colors',
            value === o.value ? 'bg-white text-brand-700 shadow-sm' : 'text-slate-500 hover:text-slate-800',
          )}
        >
          {o.label}
          {o.count !== undefined && <span className="ml-1.5 text-slate-400 tabular-nums">{o.count}</span>}
        </button>
      ))}
    </div>
  )
}

const ESTADO_ESTILO: Record<EstadoPedido, string> = {
  Registrado: 'bg-slate-100 text-slate-700 ring-slate-300',
  Confirmado: 'bg-brand-50 text-brand-700 ring-brand-200',
  Despachado: 'bg-accent-50 text-accent-700 ring-accent-200',
  Entregado: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Anulado: 'bg-red-50 text-red-700 ring-red-200',
}

export function EstadoBadge({ estado }: { estado: EstadoPedido }) {
  return (
    <span className={cx('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset', ESTADO_ESTILO[estado])}>
      <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
      {estado}
    </span>
  )
}

export function ActivoBadge({ activo }: { activo: boolean }) {
  return (
    <span
      className={cx(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset',
        activo ? 'bg-emerald-50 text-emerald-700 ring-emerald-200' : 'bg-slate-100 text-slate-500 ring-slate-300',
      )}
    >
      <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
      {activo ? 'Activo' : 'Inactivo'}
    </span>
  )
}

interface ConfirmDialogProps {
  open: boolean
  title: string
  children: ReactNode
  confirmLabel: string
  variant?: Variante
  loading?: boolean
  onConfirm: () => void
  onCancel: () => void
}

/** Diálogo modal nativo (<dialog>): foco atrapado y Escape para cerrar sin librerías. */
export function ConfirmDialog({ open, title, children, confirmLabel, variant = 'danger', loading, onConfirm, onCancel }: ConfirmDialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onCancel={(e) => {
        e.preventDefault()
        if (!loading) onCancel()
      }}
      className="m-auto w-[min(28rem,calc(100vw-2rem))] rounded-2xl bg-white p-0 shadow-(--shadow-elevated) backdrop:bg-brand-950/40 backdrop:backdrop-blur-[2px]"
    >
      <div className="flex gap-4 p-6">
        <div className={cx('grid size-10 shrink-0 place-items-center rounded-full', variant === 'danger' ? 'bg-red-50 text-red-600' : 'bg-brand-50 text-brand-600')}>
          <span className="text-lg font-bold" aria-hidden="true">
            !
          </span>
        </div>
        <div>
          <h2 id={titleId} className="text-base font-semibold text-slate-900">
            {title}
          </h2>
          <div className="mt-2 text-sm text-slate-600">{children}</div>
        </div>
      </div>
      <div className="flex justify-end gap-2 rounded-b-2xl bg-slate-50 px-6 py-3">
        <Button variant="secondary" onClick={onCancel} disabled={loading}>
          Cancelar
        </Button>
        <Button variant={variant} onClick={onConfirm} loading={loading} autoFocus>
          {confirmLabel}
        </Button>
      </div>
    </dialog>
  )
}

interface DrawerProps {
  open: boolean
  title: string
  subtitle?: string
  onClose: () => void
  children: ReactNode
  footer: ReactNode
}

/** Panel lateral modal para formularios de mantenedores: no se pierde el contexto de la lista. */
export function Drawer({ open, title, subtitle, onClose, children, footer }: DrawerProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onCancel={(e) => {
        e.preventDefault()
        onClose()
      }}
      className="drawer w-full max-w-lg bg-white p-0 shadow-2xl backdrop:bg-brand-950/40 backdrop:backdrop-blur-[2px]"
    >
      {open && (
        <div className="animate-drawer-in flex h-full flex-col">
          <header className="flex items-start justify-between gap-4 border-b border-slate-200 px-6 py-5">
            <div>
              <h2 id={titleId} className="text-lg font-semibold text-slate-900">
                {title}
              </h2>
              {subtitle && <p className="mt-0.5 text-sm text-slate-500">{subtitle}</p>}
            </div>
            <button onClick={onClose} className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Cerrar">
              <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
              </svg>
            </button>
          </header>
          <div className="flex-1 overflow-y-auto px-6 py-5">{children}</div>
          <footer className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-6 py-4">{footer}</footer>
        </div>
      )}
    </dialog>
  )
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx('rounded-xl bg-white shadow-(--shadow-card) ring-1 ring-slate-200/80', className)}>{children}</div>
}

export function PageHeader({ titulo, descripcion, acciones, eyebrow }: { titulo: ReactNode; descripcion?: ReactNode; acciones?: ReactNode; eyebrow?: string }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div>
        {eyebrow && <p className="text-xs font-semibold tracking-wider text-accent-600 uppercase">{eyebrow}</p>}
        <h1 className="mt-1 text-2xl font-bold tracking-tight text-slate-900">{titulo}</h1>
        {descripcion && <p className="mt-1 text-sm text-slate-500">{descripcion}</p>}
      </div>
      {acciones && <div className="flex flex-wrap gap-2">{acciones}</div>}
    </div>
  )
}

export function Alert({ tone = 'error', children }: { tone?: 'error' | 'warning' | 'info'; children: ReactNode }) {
  const estilos = {
    error: 'bg-red-50 text-red-800 ring-red-200 border-red-500',
    warning: 'bg-accent-50 text-accent-900 ring-accent-200 border-accent-500',
    info: 'bg-brand-50 text-brand-900 ring-brand-200 border-brand-600',
  }
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={cx('rounded-lg border-l-4 px-4 py-3 text-sm ring-1 ring-inset', estilos[tone])}>
      {children}
    </div>
  )
}

interface PaginacionProps {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPage: (page: number) => void
  onPageSize: (size: number) => void
}

export function Paginacion({ page, pageSize, totalCount, totalPages, onPage, onPageSize }: PaginacionProps) {
  if (totalCount === 0) return null
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600">
      <div className="flex items-center gap-2">
        <span>Mostrar</span>
        <Select aria-label="Filas por página" value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))} className="w-20 py-1">
          {[10, 20, 50].map((n) => (
            <option key={n}>{n}</option>
          ))}
        </Select>
        <span className="hidden sm:inline">
          · {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} de {totalCount}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          Anterior
        </Button>
        <span className="tabular-nums">
          {page} / {totalPages}
        </span>
        <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>
          Siguiente
        </Button>
      </div>
    </div>
  )
}

export function EmptyState({ titulo, descripcion, accion }: { titulo: string; descripcion: string; accion?: ReactNode }) {
  return (
    <div className="px-6 py-16 text-center">
      <div className="mx-auto grid size-12 place-items-center rounded-full bg-brand-50 text-brand-600">
        <svg viewBox="0 0 24 24" className="size-6" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
        </svg>
      </div>
      <p className="mt-4 font-semibold text-slate-900">{titulo}</p>
      <p className="mt-1 text-sm text-slate-500">{descripcion}</p>
      {accion && <div className="mt-5">{accion}</div>}
    </div>
  )
}

export function SkeletonRows({ filas = 5, columnas }: { filas?: number; columnas: number }) {
  return (
    <>
      {Array.from({ length: filas }).map((_, i) => (
        <tr key={i}>
          {Array.from({ length: columnas }).map((__, j) => (
            <td key={j} className="px-4 py-4">
              <div className="h-4 animate-pulse rounded bg-slate-100" />
            </td>
          ))}
        </tr>
      ))}
    </>
  )
}

export { cx }
