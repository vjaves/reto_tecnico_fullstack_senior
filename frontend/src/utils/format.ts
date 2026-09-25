const SIMBOLOS: Record<string, string> = { PEN: 'S/', USD: 'US$' }

export function formatMoney(valor: number, moneda = 'PEN'): string {
  const simbolo = SIMBOLOS[moneda] ?? moneda
  return `${simbolo} ${valor.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

/** "2025-01-10" → "10/01/2025". Se parte el string para no sufrir el corrimiento de zona horaria de new Date(). */
export function formatFecha(iso: string): string {
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${d}/${m}/${y}`
}

export function hoyIso(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** Copia del objeto sin una clave (para limpiar el error de un campo al editarlo). */
export function sinClave<T extends Record<string, unknown>>(obj: T, clave: string): T {
  const copia = { ...obj }
  delete copia[clave]
  return copia
}

export function redondear2(valor: number): number {
  return Math.round((valor + Number.EPSILON) * 100) / 100
}
