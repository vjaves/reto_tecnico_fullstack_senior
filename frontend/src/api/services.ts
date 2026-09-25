import type {
  ActualizarUsuarioRequest,
  CrearUsuarioRequest,
  EstadoSistema,
  UsuarioAdmin,
  Cliente,
  ClienteDetalle,
  ClienteResumen,
  GuardarClienteRequest,
  GuardarProductoRequest,
  ItemCatalogo,
  LoginResponse,
  MaestroFiltro,
  PagedResult,
  Pedido,
  PedidoFiltro,
  PedidoRequest,
  PedidoResumen,
  PedidoResumenEstados,
  Producto,
  ProductoDetalle,
  ProductoResumen,
} from '../types'
import { http } from './http'

/** Sólo se envían los filtros con valor: la URL queda limpia y el backend usa sus defaults. */
const soloConValor = (filtro: object) =>
  Object.fromEntries(Object.entries(filtro).filter(([, v]) => v !== '' && v !== undefined && v !== null))

export const authApi = {
  login: (email: string, password: string) =>
    http.post<LoginResponse>('/auth/login', { email, password }).then((r) => r.data),
}

export const pedidosApi = {
  listar: (filtro: PedidoFiltro, signal?: AbortSignal) =>
    http.get<PagedResult<PedidoResumen>>('/api/pedidos', { params: soloConValor(filtro), signal }).then((r) => r.data),
  resumen: (signal?: AbortSignal) => http.get<PedidoResumenEstados>('/api/pedidos/resumen', { signal }).then((r) => r.data),
  obtener: (id: number, signal?: AbortSignal) => http.get<Pedido>(`/api/pedidos/${id}`, { signal }).then((r) => r.data),
  siguienteNumero: () => http.get<{ numeroPedido: string }>('/api/pedidos/siguiente-numero').then((r) => r.data.numeroPedido),
  crear: (pedido: PedidoRequest) => http.post<Pedido>('/api/pedidos', pedido).then((r) => r.data),
  actualizar: (id: number, pedido: PedidoRequest) => http.put<Pedido>(`/api/pedidos/${id}`, pedido).then((r) => r.data),
  eliminar: (id: number) => http.delete(`/api/pedidos/${id}`).then(() => undefined),
}

export const clientesApi = {
  listar: (filtro: MaestroFiltro, signal?: AbortSignal) =>
    http.get<PagedResult<ClienteResumen>>('/api/clientes', { params: soloConValor(filtro), signal }).then((r) => r.data),
  obtener: (id: number) => http.get<ClienteDetalle>(`/api/clientes/${id}`).then((r) => r.data),
  crear: (c: GuardarClienteRequest) => http.post<ClienteDetalle>('/api/clientes', c).then((r) => r.data),
  actualizar: (id: number, c: GuardarClienteRequest) => http.put<ClienteDetalle>(`/api/clientes/${id}`, c).then((r) => r.data),
  eliminar: (id: number) => http.delete(`/api/clientes/${id}`).then(() => undefined),
}

export const productosApi = {
  listar: (filtro: MaestroFiltro, signal?: AbortSignal) =>
    http.get<PagedResult<ProductoResumen>>('/api/productos', { params: soloConValor(filtro), signal }).then((r) => r.data),
  obtener: (id: number) => http.get<ProductoDetalle>(`/api/productos/${id}`).then((r) => r.data),
  crear: (p: GuardarProductoRequest) => http.post<ProductoDetalle>('/api/productos', p).then((r) => r.data),
  actualizar: (id: number, p: GuardarProductoRequest) => http.put<ProductoDetalle>(`/api/productos/${id}`, p).then((r) => r.data),
  eliminar: (id: number) => http.delete(`/api/productos/${id}`).then(() => undefined),
}

export const adminApi = {
  usuarios: (buscar?: string) => http.get<UsuarioAdmin[]>('/api/admin/usuarios', { params: buscar ? { buscar } : {} }).then((r) => r.data),
  crearUsuario: (u: CrearUsuarioRequest) => http.post<UsuarioAdmin>('/api/admin/usuarios', u).then((r) => r.data),
  actualizarUsuario: (id: number, u: ActualizarUsuarioRequest) => http.put<UsuarioAdmin>(`/api/admin/usuarios/${id}`, u).then((r) => r.data),
  desbloquear: (id: number) => http.post<UsuarioAdmin>(`/api/admin/usuarios/${id}/desbloquear`).then((r) => r.data),
  restablecerClave: (id: number, nuevaClave: string) =>
    http.post(`/api/admin/usuarios/${id}/restablecer-clave`, { nuevaClave }).then(() => undefined),
  sistema: (signal?: AbortSignal) => http.get<EstadoSistema>('/api/admin/sistema', { signal }).then((r) => r.data),
}

export const catalogosApi = {
  clientes: () => http.get<Cliente[]>('/api/catalogos/clientes').then((r) => r.data),
  productos: () => http.get<Producto[]>('/api/catalogos/productos').then((r) => r.data),
  monedas: () => http.get<ItemCatalogo[]>('/api/catalogos/monedas').then((r) => r.data),
  sucursales: () => http.get<ItemCatalogo[]>('/api/catalogos/sucursales').then((r) => r.data),
  tiposDocumento: () => http.get<ItemCatalogo[]>('/api/catalogos/tipos-documento').then((r) => r.data),
  unidadesMedida: () => http.get<ItemCatalogo[]>('/api/catalogos/unidades-medida').then((r) => r.data),
}
