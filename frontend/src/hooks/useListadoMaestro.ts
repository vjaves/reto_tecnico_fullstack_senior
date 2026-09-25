import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { toApiError, type ApiError } from '../api/http'
import type { FiltroActivo, MaestroFiltro, PagedResult } from '../types'
import { useDebounce } from './useDebounce'

type Listar<T> = (filtro: MaestroFiltro, signal: AbortSignal) => Promise<PagedResult<T>>

/**
 * Estado de un listado de mantenedor: filtros en la URL (compartibles, sobreviven a F5),
 * búsqueda con debounce, carga cancelable y recarga manual tras guardar o eliminar.
 * `listar` debe ser una referencia estable (las funciones de api/services lo son).
 */
export function useListadoMaestro<T>(listar: Listar<T>) {
  const [params, setParams] = useSearchParams()

  const filtro: MaestroFiltro = useMemo(
    () => ({
      buscar: params.get('buscar') ?? '',
      estado: (params.get('estado') as FiltroActivo | null) ?? '',
      page: Math.max(Number(params.get('page')) || 1, 1),
      pageSize: Number(params.get('pageSize')) || 10,
    }),
    [params],
  )

  const actualizar = useCallback(
    (cambios: Partial<MaestroFiltro>) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          for (const [k, v] of Object.entries(cambios)) {
            if (v === '' || v === undefined) next.delete(k)
            else next.set(k, String(v))
          }
          // Cambiar un filtro vuelve a la primera página; paginar no.
          if (cambios.page === undefined) next.delete('page')
          return next
        },
        { replace: true },
      )
    },
    [setParams],
  )

  const [busqueda, setBusqueda] = useState(filtro.buscar ?? '')
  const busquedaDebounced = useDebounce(busqueda)
  useEffect(() => {
    if (busquedaDebounced !== (filtro.buscar ?? '')) actualizar({ buscar: busquedaDebounced })
  }, [busquedaDebounced, filtro.buscar, actualizar])

  const [data, setData] = useState<PagedResult<T> | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setCargando(true)
    setError(null)
    listar(filtro, controller.signal)
      .then((r) => {
        // Si al eliminar se vació la última página, se retrocede una.
        if (r.items.length === 0 && r.page > 1 && r.totalCount > 0) actualizar({ page: r.totalPages })
        else setData(r)
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(toApiError(e))
      })
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false)
      })
    return () => controller.abort()
  }, [filtro, version, listar, actualizar])

  const recargar = useCallback(() => setVersion((v) => v + 1), [])
  const limpiar = useCallback(() => {
    setBusqueda('')
    setParams({}, { replace: true })
  }, [setParams])

  return { filtro, actualizar, busqueda, setBusqueda, data, cargando, error, recargar, limpiar }
}
