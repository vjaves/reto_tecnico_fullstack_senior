import { useId, useMemo, useRef, useState } from 'react'
import type { Cliente } from '../types'
import { cx, Input } from './ui'

interface Props {
  id?: string
  clientes: Cliente[]
  value: number
  onChange: (id: number) => void
  invalid?: boolean
  disabled?: boolean
  describedBy?: string
}

const normalizar = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()

/**
 * Combobox accesible (patrón ARIA 1.2) con búsqueda por nombre o documento, sin tildes ni mayúsculas.
 * Flechas para moverse, Enter para elegir, Escape para cerrar.
 */
export function ClienteCombobox({ id, clientes, value, onChange, invalid, disabled, describedBy }: Props) {
  const seleccionado = clientes.find((c) => c.id === value)
  const [texto, setTexto] = useState<string | null>(null) // null = mostrando el seleccionado
  const [abierto, setAbierto] = useState(false)
  const [activo, setActivo] = useState(0)
  const listaId = useId()
  const inputRef = useRef<HTMLInputElement>(null)

  const opciones = useMemo(() => {
    if (!texto) return clientes
    const q = normalizar(texto)
    return clientes.filter((c) => normalizar(c.nombre).includes(q) || c.numeroDocumento.includes(q))
  }, [clientes, texto])

  function elegir(c: Cliente) {
    onChange(c.id)
    setTexto(null)
    setAbierto(false)
  }

  return (
    <div className="relative">
      <Input
        ref={inputRef}
        id={id}
        role="combobox"
        aria-expanded={abierto}
        aria-controls={listaId}
        aria-autocomplete="list"
        aria-describedby={describedBy}
        aria-activedescendant={abierto && opciones[activo] ? `${listaId}-${opciones[activo].id}` : undefined}
        autoComplete="off"
        disabled={disabled}
        invalid={invalid}
        placeholder="Buscar cliente por nombre o documento…"
        value={texto ?? (seleccionado ? `${seleccionado.nombre} · ${seleccionado.numeroDocumento}` : '')}
        onFocus={(e) => {
          e.target.select()
          setAbierto(true)
        }}
        onBlur={() => {
          // Se difiere para que el click en una opción llegue antes de cerrar.
          window.setTimeout(() => {
            setAbierto(false)
            setTexto(null)
          }, 120)
        }}
        onChange={(e) => {
          setTexto(e.target.value)
          setActivo(0)
          setAbierto(true)
        }}
        onKeyDown={(e) => {
          if (e.key === 'ArrowDown') {
            e.preventDefault()
            setAbierto(true)
            setActivo((i) => Math.min(i + 1, opciones.length - 1))
          } else if (e.key === 'ArrowUp') {
            e.preventDefault()
            setActivo((i) => Math.max(i - 1, 0))
          } else if (e.key === 'Enter' && abierto && opciones[activo]) {
            e.preventDefault()
            elegir(opciones[activo])
          } else if (e.key === 'Escape') {
            setAbierto(false)
            setTexto(null)
          }
        }}
      />
      {abierto && !disabled && (
        <ul
          id={listaId}
          role="listbox"
          className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-lg bg-white py-1 text-sm shadow-lg ring-1 ring-slate-200"
        >
          {opciones.length === 0 && <li className="px-3 py-2 text-slate-500">Sin coincidencias</li>}
          {opciones.map((c, i) => (
            <li
              key={c.id}
              id={`${listaId}-${c.id}`}
              role="option"
              aria-selected={c.id === value}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => elegir(c)}
              onMouseEnter={() => setActivo(i)}
              className={cx('flex cursor-pointer justify-between gap-3 px-3 py-2', i === activo && 'bg-brand-50', c.id === value && 'font-medium')}
            >
              <span className="truncate">{c.nombre}</span>
              <span className="shrink-0 text-xs text-slate-500 tabular-nums">{c.numeroDocumento}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
