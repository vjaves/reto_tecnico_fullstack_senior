// Contratos de la API. Se mantienen alineados a mano con los DTO de Atlantic.Pedidos.Application.

export const ESTADOS = ['Registrado', 'Confirmado', 'Despachado', 'Entregado', 'Anulado'] as const
export type EstadoPedido = (typeof ESTADOS)[number]

/** Estados que el usuario puede elegir al editar (Anulado sólo se alcanza eliminando). */
export const ESTADOS_EDITABLES: EstadoPedido[] = ['Registrado', 'Confirmado', 'Despachado', 'Entregado']

export type Rol = 'Admin' | 'User'

export interface UsuarioSesion {
  id: number
  nombre: string
  email: string
  rol: Rol
}

export interface LoginResponse {
  token: string
  expiresIn: number
  tokenType: string
  usuario: UsuarioSesion
}

export interface PedidoResumen {
  id: number
  numeroPedido: string
  clienteId: number
  cliente: string
  fecha: string
  moneda: string
  total: number
  estado: EstadoPedido
  cantidadLineas: number
}

export interface PedidoDetalle {
  id: number
  item: number
  productoId: number
  codigoProducto: string | null
  producto: string
  cantidad: number
  precioVenta: number
  descuento: number
  subTotal: number
  importeTotal: number
}

export interface Pedido {
  id: number
  numeroPedido: string
  clienteId: number
  cliente: string
  sucursalId: number
  sucursal: string
  monedaId: number
  moneda: string
  fecha: string
  totalDescuentos: number
  total: number
  estado: EstadoPedido
  fechaCreacion: string
  fechaModificacion: string | null
  rowVersion: string
  detalles: PedidoDetalle[]
}

export interface PedidoLineaRequest {
  id: number | null
  productoId: number
  cantidad: number
  precioVenta: number
  descuento: number
}

export interface PedidoRequest {
  numeroPedido: string
  clienteId: number
  sucursalId: number
  monedaId: number
  fecha: string
  detalles: PedidoLineaRequest[]
  estado?: EstadoPedido
  rowVersion?: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface PedidoFiltro {
  buscar?: string
  estado?: EstadoPedido | ''
  desde?: string
  hasta?: string
  page: number
  pageSize: number
  ordenarPor?: 'numero' | 'cliente' | 'fecha' | 'total' | 'estado'
  descendente: boolean
}

export interface ItemCatalogo {
  id: number
  nombre: string
  abreviatura?: string | null
}

export interface Cliente {
  id: number
  numeroDocumento: string
  nombre: string
}

export interface Producto {
  id: number
  codigo: string | null
  nombre: string
}

export interface PedidoResumenEstados {
  total: number
  porEstado: Record<string, number>
}

// ---- Administración

export interface UsuarioAdmin {
  id: number
  nombreUsuario: string
  nombreCompleto: string
  numeroDocumento: string
  email: string
  rol: Rol
  activo: boolean
  bloqueado: boolean
  intentosFallidos: number
  fechaUltimoLogin: string | null
  fechaCreacion: string | null
}

export interface CrearUsuarioRequest {
  nombreUsuario: string
  nombreCompleto: string
  numeroDocumento: string
  email: string
  rol: Rol
  clave: string
}

export interface ActualizarUsuarioRequest {
  nombreCompleto: string
  numeroDocumento: string
  email: string
  rol: Rol
  activo: boolean
}

export type EstadoSalud = 'Healthy' | 'Degraded' | 'Unhealthy'
export type EstadoCircuito = 'Closed' | 'Open' | 'HalfOpen' | 'Isolated'

export interface EstadoSistema {
  estado: EstadoSalud
  entorno: string
  version: string
  inicioUtc: string
  segundosActivo: number
  circuitoSql: EstadoCircuito
  chequeos: { nombre: string; estado: EstadoSalud; descripcion: string | null; duracionMs: number }[]
  configuracion: Record<string, string>
}

// ---- Mantenedores

export type FiltroActivo = '' | 'activos' | 'inactivos'

export interface MaestroFiltro {
  buscar?: string
  estado?: FiltroActivo
  page: number
  pageSize: number
}

export interface ClienteResumen {
  id: number
  tipoDocumento: string
  numeroDocumento: string
  nombre: string
  celular: string | null
  activo: boolean
  pedidosEnCurso: number
}

export interface ClienteDetalle {
  id: number
  tipoDocumentoId: number
  tipoDocumento: string
  numeroDocumento: string
  primerNombre: string | null
  segundoNombre: string | null
  apellidoPaterno: string | null
  apellidoMaterno: string | null
  razonSocial: string | null
  nombreComercial: string | null
  direccion: string | null
  celular: string | null
  activo: boolean
  fechaCreacion: string | null
  fechaModificacion: string | null
}

export interface GuardarClienteRequest {
  tipoDocumentoId: number
  numeroDocumento: string
  primerNombre: string
  segundoNombre: string
  apellidoPaterno: string
  apellidoMaterno: string
  razonSocial: string
  nombreComercial: string
  direccion: string
  celular: string
  activo: boolean
}

export interface ProductoResumen {
  id: number
  codigo: string | null
  nombre: string
  unidadMedida: string | null
  activo: boolean
  pedidosEnCurso: number
}

export interface ProductoDetalle {
  id: number
  codigo: string | null
  nombre: string
  descripcion: string | null
  unidadMedidaId: number | null
  unidadMedida: string | null
  activo: boolean
  fechaCreacion: string
  fechaModificacion: string | null
}

export interface GuardarProductoRequest {
  codigo: string
  nombre: string
  descripcion: string
  unidadMedidaId: number
  activo: boolean
}
