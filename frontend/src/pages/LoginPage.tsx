import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { toApiError } from '../api/http'
import { useAuth } from '../auth/AuthContext'
import { IconLock } from '../components/icons'
import { Alert, Button, Field, Input, Logo } from '../components/ui'

const USUARIOS_DEMO = [
  { email: 'admin@email.com', rol: 'Admin', detalle: 'puede eliminar' },
  { email: 'user@email.com', rol: 'User', detalle: 'no puede eliminar' },
]

export function LoginPage() {
  const { usuario, login, motivoCierre } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const destino = (location.state as { from?: string } | null)?.from ?? '/pedidos'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [verClave, setVerClave] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errores, setErrores] = useState<Record<string, string[]>>({})

  if (usuario) return <Navigate to={destino} replace />

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setEnviando(true)
    setError(null)
    setErrores({})
    try {
      await login(email.trim(), password)
      navigate(destino, { replace: true })
    } catch (err) {
      const apiError = toApiError(err)
      setError(apiError.message)
      setErrores(apiError.fieldErrors)
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center p-6">
      <section className="w-full max-w-md rounded-2xl bg-white p-8 shadow-(--shadow-elevated) ring-1 ring-slate-200/80 sm:p-10">
        <div>
          <div className="mb-8 flex items-center gap-2">
            <Logo className="size-10" />
            <span className="text-lg font-bold text-slate-900">Atlantic</span>
          </div>
          <h2 className="text-2xl font-bold tracking-tight text-slate-900">Iniciar sesión</h2>
          <p className="mt-1 text-sm text-slate-500">Ingresa con tu correo corporativo.</p>

          <div className="mt-6 space-y-4">
            {motivoCierre && !error && <Alert tone="warning">{motivoCierre}</Alert>}
            {error && <Alert>{error}</Alert>}

            <form onSubmit={onSubmit} noValidate className="space-y-4">
              <Field label="Correo electrónico" error={errores.email?.[0]}>
                {(id, desc) => (
                  <Input
                    id={id}
                    aria-describedby={desc}
                    type="email"
                    autoComplete="username"
                    autoFocus
                    required
                    value={email}
                    invalid={!!errores.email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="usuario@email.com"
                  />
                )}
              </Field>

              <Field label="Contraseña" error={errores.password?.[0]}>
                {(id, desc) => (
                  <div className="relative">
                    <Input
                      id={id}
                      aria-describedby={desc}
                      type={verClave ? 'text' : 'password'}
                      autoComplete="current-password"
                      required
                      value={password}
                      invalid={!!errores.password}
                      onChange={(e) => setPassword(e.target.value)}
                      className="pr-16"
                    />
                    <button
                      type="button"
                      onClick={() => setVerClave((v) => !v)}
                      className="absolute inset-y-0 right-0 px-3 text-xs font-medium text-slate-500 hover:text-slate-800"
                      aria-pressed={verClave}
                    >
                      {verClave ? 'Ocultar' : 'Mostrar'}
                    </button>
                  </div>
                )}
              </Field>

              <Button type="submit" loading={enviando} className="w-full" icon={<IconLock className="size-4" />}>
                Ingresar
              </Button>
            </form>

            <div className="rounded-lg border border-dashed border-brand-200 bg-brand-50/50 p-3 text-xs text-slate-500">
              <p className="mb-2 font-medium text-slate-600">Usuarios de prueba (clave 123456)</p>
              <ul className="space-y-1">
                {USUARIOS_DEMO.map((u) => (
                  <li key={u.email}>
                    <button
                      type="button"
                      className="text-brand-700 hover:underline"
                      onClick={() => {
                        setEmail(u.email)
                        setPassword('123456')
                      }}
                    >
                      {u.email}
                    </button>{' '}
                    · {u.rol}, {u.detalle}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      </section>
    </div>
  )
}
