import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { setAccessToken, setUnauthorizedHandler } from '../api/http'
import { authApi } from '../api/services'
import type { UsuarioSesion } from '../types'

interface Sesion {
  token: string
  expiraEn: number // epoch ms
  usuario: UsuarioSesion
}

interface AuthContextValue {
  usuario: UsuarioSesion | null
  expiraEn: number | null
  esAdmin: boolean
  /** Motivo del último cierre de sesión forzado, para mostrarlo en el login. */
  motivoCierre: string | null
  login: (email: string, password: string) => Promise<void>
  logout: (motivo?: string) => void
}

const STORAGE_KEY = 'atlantic.sesion'
const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * La sesión se guarda en sessionStorage: sobrevive a un F5 pero no queda en el equipo al cerrar la pestaña.
 * La expiración se toma del claim `exp` del JWT; si el token ya venció al cargar, no se restaura.
 */
function leerSesion(): Sesion | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const sesion = JSON.parse(raw) as Sesion
    return sesion.expiraEn > Date.now() ? sesion : null
  } catch {
    return null
  }
}

function expiracionDelToken(token: string, expiresInSegundos: number): number {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number }
    if (payload.exp) return payload.exp * 1000
  } catch {
    // token no decodificable: se usa expiresIn
  }
  return Date.now() + expiresInSegundos * 1000
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null>(() => {
    // El token restaurado se entrega al cliente HTTP antes del primer render de los hijos:
    // sus efectos (la primera carga de la lista) corren antes que los de este provider.
    const restaurada = leerSesion()
    setAccessToken(restaurada?.token ?? null)
    return restaurada
  })
  const [motivoCierre, setMotivoCierre] = useState<string | null>(null)

  const logout = useCallback((motivo?: string) => {
    sessionStorage.removeItem(STORAGE_KEY)
    setAccessToken(null)
    setMotivoCierre(motivo ?? null)
    setSesion(null)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const r = await authApi.login(email, password)
    const nueva: Sesion = { token: r.token, expiraEn: expiracionDelToken(r.token, r.expiresIn), usuario: r.usuario }
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(nueva))
    setAccessToken(nueva.token)
    setMotivoCierre(null)
    setSesion(nueva)
  }, [])

  // Un 401 fuera del login significa token vencido o inválido: se cierra la sesión y el
  // ProtectedRoute lleva al login recordando la pantalla actual.
  useEffect(() => {
    setUnauthorizedHandler((code, message) =>
      logout(
        code === 'TOKEN_EXPIRADO'
          ? 'Tu sesión expiró. Vuelve a iniciar sesión.'
          : // Revocada por el administrador (rol, acceso o clave cambiados): el servidor explica el motivo.
            code === 'SESION_REVOCADA'
            ? message
            : 'Tu sesión ya no es válida.',
      ),
    )
  }, [logout])

  // Cierre automático al vencer el token, aunque el usuario no haga ninguna petición.
  useEffect(() => {
    if (!sesion) return
    const restante = sesion.expiraEn - Date.now()
    const timer = window.setTimeout(() => logout('Tu sesión expiró. Vuelve a iniciar sesión.'), Math.max(restante, 0))
    return () => window.clearTimeout(timer)
  }, [sesion, logout])

  const value = useMemo<AuthContextValue>(
    () => ({
      usuario: sesion?.usuario ?? null,
      expiraEn: sesion?.expiraEn ?? null,
      esAdmin: sesion?.usuario.rol === 'Admin',
      motivoCierre,
      login,
      logout,
    }),
    [sesion, motivoCierre, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return ctx
}
