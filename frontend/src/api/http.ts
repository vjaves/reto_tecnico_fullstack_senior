import axios, { AxiosError } from 'axios'

/**
 * Error normalizado a partir del ProblemDetails de la API. Las pantallas sólo manejan esto,
 * nunca un AxiosError crudo.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly fieldErrors: Record<string, string[]>
  readonly traceId?: string

  constructor(status: number, code: string, message: string, fieldErrors: Record<string, string[]> = {}, traceId?: string) {
    super(message)
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
    this.traceId = traceId
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  code?: string
  errors?: Record<string, string[]>
  traceId?: string
}

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
})

// AuthContext es el dueño de la sesión: fija aquí el token vigente (en el mismo instante del login/logout,
// no en un efecto, para que ninguna petición salga con un token viejo) y quién atiende los 401.
let accessToken: string | null = null
let onUnauthorized: (code: string, message: string) => void = () => {}

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function setUnauthorizedHandler(handler: (code: string, message: string) => void) {
  onUnauthorized = handler
}

http.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    if (!error.response) {
      const offline = error.code === 'ECONNABORTED' ? 'La API tardó demasiado en responder.' : 'No se pudo conectar con la API.'
      return Promise.reject(new ApiError(0, 'SIN_CONEXION', offline))
    }

    const { status, data } = error.response
    const apiError = new ApiError(
      status,
      data?.code ?? `HTTP_${status}`,
      data?.detail ?? data?.title ?? 'Ocurrió un error inesperado.',
      data?.errors ?? {},
      data?.traceId,
    )

    // Un 401 en /auth/login es "credenciales inválidas", no una sesión vencida.
    if (status === 401 && !error.config?.url?.startsWith('/auth/')) onUnauthorized(apiError.code, apiError.message)

    return Promise.reject(apiError)
  },
)

export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError(0, 'DESCONOCIDO', 'Ocurrió un error inesperado.')
}
