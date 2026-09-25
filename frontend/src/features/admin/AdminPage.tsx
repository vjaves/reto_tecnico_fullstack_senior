import { useSearchParams } from 'react-router'
import { PageHeader, cx } from '../../components/ui'
import { SistemaTab } from './SistemaTab'
import { UsuariosTab } from './UsuariosTab'

const TABS = [
  { id: 'usuarios', label: 'Usuarios y accesos' },
  { id: 'sistema', label: 'Estado del sistema' },
] as const

type Tab = (typeof TABS)[number]['id']

export function AdminPage() {
  const [params, setParams] = useSearchParams()
  const tab: Tab = params.get('tab') === 'sistema' ? 'sistema' : 'usuarios'

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Administración"
        titulo="Panel de administración"
        descripcion="Usuarios, roles y accesos; salud de la base de datos y de los mecanismos de resiliencia."
      />

      <div role="tablist" aria-label="Secciones del panel" className="flex gap-1 border-b border-slate-200">
        {TABS.map((t) => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => setParams({ tab: t.id }, { replace: true })}
            className={cx(
              '-mb-px border-b-2 px-4 py-2.5 text-sm font-semibold transition-colors',
              tab === t.id ? 'border-accent-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-800',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div role="tabpanel">{tab === 'usuarios' ? <UsuariosTab /> : <SistemaTab />}</div>
    </div>
  )
}
