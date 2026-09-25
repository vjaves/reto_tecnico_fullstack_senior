import { useEffect, useState } from 'react'

export function useDebounce<T>(valor: T, ms = 350): T {
  const [debounced, setDebounced] = useState(valor)
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(valor), ms)
    return () => window.clearTimeout(id)
  }, [valor, ms])
  return debounced
}
