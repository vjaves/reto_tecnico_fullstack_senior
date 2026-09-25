# Atlantic Pedidos

Solución fullstack para el reto técnico: **API REST en .NET 9** con autenticación JWT y CRUD de pedidos, y **SPA en React 19** que la consume.

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core 9, Clean Architecture, EF Core 9 (SQL Server), **Polly v8** (retry + circuit breaker + timeout), FluentValidation, Serilog, JWT Bearer, BCrypt, Rate Limiting nativo |
| Frontend | React 19 + TypeScript, Vite (HTTPS), React Router, Tailwind CSS v4, Axios |
| Base de datos | SQL Server (`BDExamenAtlantic`), migraciones automáticas de EF Core |
| Pruebas | xUnit + NSubstitute + FluentAssertions (50 unitarias), colección Postman con tests (45 requests, 39 asserts) |

Además del CRUD de pedidos incluye **mantenedores de clientes y productos** y un **Panel de Administración** (usuarios, roles, accesos y estado del sistema).

### Cumplimiento del diagrama de arquitectura

| Elemento del diagrama | Implementación |
|---|---|
| Cliente web: Inicio de sesión | `LoginPage` y `AuthContext`: JWT en `sessionStorage`, cierre automático al vencer |
| Cliente web: Gestión de pedidos (CRUD) | `PedidosListPage` y `PedidoFormPage` |
| Cliente web: **Panel de Administración** | `/admin`, solo rol Admin: usuarios y roles (alta, edición, desactivar, desbloquear, restablecer clave) y estado del sistema |
| **HTTPS / JWT Token** | Navegador ⇄ Vite y Vite ⇄ API por **HTTPS** con el certificado de desarrollo de ASP.NET. La API redirige HTTP → HTTPS; HSTS fuera de Development |
| API: Autenticación / Login | `POST /auth/login`: BCrypt, bloqueo por intentos, JWT con expiración y roles |
| API: API REST de pedidos (CRUD) | `api/pedidos`: GET, POST, PUT y DELETE (baja lógica) |
| API: **Seguridad y Control** | Política por defecto que exige token (`FallbackPolicy`), rol Admin para eliminar y administrar, **control de sesión en cada request**, headers de seguridad y CORS restringido |
| **Rate Limiting** | Global: token bucket por usuario (o IP), 300/min. Login: 10/min por IP. Responde 429 con `Retry-After` |
| **JWT Auth** | JWT Bearer con emisor, audiencia, firma HS256 y vigencia validados; claims `sub`, `role`, `empresa_id` e `iat_ms` |
| **Circuit Breaker** | Polly: se abre con ≥ 50 % de fallos en 60 s (mínimo 5 ejecuciones) y queda abierto 20 s. Mientras tanto, 503 inmediato con `Retry-After` |
| **Retry Policies** | Polly: 3 reintentos con backoff exponencial y jitter, solo ante errores transitorios de SQL Server |
| Base de datos SQL Server | EF Core con migraciones automáticas sobre el modelo referencial |

---

## 1. Puesta en marcha

### Requisitos

- .NET SDK 9
- Node.js 20 o superior
- SQL Server (Express sirve) con autenticación de Windows

### Paso 1: base de datos

La cadena de conexión está en `backend/src/Atlantic.Pedidos.Api/appsettings.json`, con el nombre `miConexion`:

```
Data Source=DESKTOP-0V9BORH\SQLEXPRESS;Initial Catalog=BDExamenAtlantic;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;
```

Si su instancia tiene otro nombre, cambie `Data Source`. **No hace falta ejecutar scripts**: la API aplica las migraciones al iniciar. La primera vez crea o completa el esquema y carga los datos de prueba.

Si la base todavía no existe, créela primero:

```powershell
sqlcmd -S "DESKTOP-0V9BORH\SQLEXPRESS" -E -C -I -f 65001 -i database\00_crear_base_datos.sql
```

### Paso 2: certificado HTTPS de desarrollo (una sola vez)

```powershell
dotnet dev-certs https --trust
```

### Paso 3: API

```powershell
cd backend
dotnet run --project src/Atlantic.Pedidos.Api --launch-profile https
```

- API: https://localhost:7080 (`http://localhost:5080` redirige a HTTPS)
- Swagger: https://localhost:7080/swagger (botón **Authorize**: pegar el token sin "Bearer")
- Health check: https://localhost:7080/health (BD y circuit breaker)

### Paso 4: Frontend

```powershell
cd frontend
npm install
npm run certs   # exporta el certificado de desarrollo para que Vite sirva por HTTPS
npm run dev
```

Abrir **https://localhost:5173**. Vite reenvía `/api` y `/auth` a la API por HTTPS (ver `vite.config.ts`), así que no hay que configurar URLs. Sin `npm run certs`, Vite sirve por HTTP y lo avisa en la consola.

### Usuarios de prueba

| Email | Clave | Rol | Puede eliminar |
|---|---|---|---|
| `admin@email.com` | `123456` | Admin | Sí |
| `user@email.com` | `123456` | User | No (recibe 403) |

La clave `123456` es la del ejemplo del enunciado; en BD se guarda como hash BCrypt.

### Pruebas

```powershell
cd backend
dotnet test
```

**Postman:** importar `postman/Atlantic-Pedidos.postman_collection.json` y ejecutarla completa con el Runner. Los tests encadenan token, id y rowVersion entre requests. También corre por consola:

```powershell
npx newman run postman/Atlantic-Pedidos.postman_collection.json --insecure
```

`--insecure` hace falta porque Node no usa el almacén de certificados de Windows. Postman de escritorio acepta el certificado de desarrollo sin esa opción.

---

## 2. Arquitectura

```
backend/
├─ src/
│  ├─ Atlantic.Pedidos.Domain          Entidades y reglas de negocio. Sin dependencias.
│  ├─ Atlantic.Pedidos.Application     Casos de uso, DTOs, validaciones, puertos (interfaces).
│  ├─ Atlantic.Pedidos.Infrastructure  EF Core, repositorios, migraciones, JWT, BCrypt.
│  └─ Atlantic.Pedidos.Api             Controladores, middleware, seguridad, composición (DI).
└─ tests/
   └─ Atlantic.Pedidos.UnitTests       Dominio y servicios de aplicación.
```

Las dependencias apuntan hacia adentro: `Api → Infrastructure → Application → Domain`. Application define los puertos (`IPedidoRepository`, `IUnitOfWork`, `IJwtTokenGenerator`, `IPasswordHasher`, `ICurrentUser`…) e Infrastructure los implementa. Los servicios no conocen EF Core, HTTP ni BCrypt.

### Decisiones de diseño

- **El agregado `Pedido` protege sus reglas.** Los setters son privados. Solo `Crear`, `Actualizar` y `Anular` cambian el estado, y en cada cambio se recalculan los totales. Así ninguna regla depende de que el llamador se acuerde de validarla.
- **Lectura y escritura separadas (CQRS liviano).** `IPedidoRepository` carga el agregado con tracking para modificarlo. `IPedidoQueries` proyecta directo a DTO sin tracking, con paginación, filtros y orden resueltos en SQL.
- **Tres niveles de error, cada uno con su status:**
  - Forma inválida (FluentValidation): **400**, con errores por campo.
  - Regla de negocio (dominio): **422**, con un `code` estable.
  - Conflicto con el estado actual (número duplicado, edición concurrente): **409**.
- **Manejo global de excepciones** en `GlobalExceptionHandler` (`IExceptionHandler` de .NET 8+): convierte cada excepción en `ProblemDetails` (RFC 7807) con `code` y `traceId`. Los controladores no tienen try/catch, y un 500 nunca expone el mensaje interno.
- **Multiempresa.** El JWT lleva `empresa_id` y todas las consultas se acotan a la empresa del usuario. Un pedido de otra empresa responde 404, no 403, para no revelar que existe.

### Base de datos: sobre el modelo referencial existente

`BDExamenAtlantic` ya traía las tablas del modelo referencial: `Pedido`, `PedidoDetalle`, `Cliente`, `Usuario`, `Producto`, `Moneda`, `Sucursal`… La solución **las respeta**: EF mapea sus nombres (`IDPedido`, `TotalPedido`, `FechaAnulado`…) y la migración inicial es idempotente. Crea cada tabla solo si falta, así que funciona tanto sobre esta base como sobre una vacía.

Lo que el reto exigía y el modelo no tenía se agrega en la migración `EsquemaInicial`:

| Cambio | Motivo |
|---|---|
| `Pedido.Estado` (varchar, CHECK) | Ciclo de vida del pedido |
| `Pedido.RowVersion` (rowversion) | Concurrencia optimista: dos usuarios editando el mismo pedido |
| `UX_Pedido_NumeroPedido` (único) | Número de pedido único, garantizado también en BD |
| `CK_Pedido_TotalPedido` (`> 0`) | La regla del total, también como restricción de BD |
| `CK_Pedido_Anulacion` | `Estado = 'Anulado'` si y solo si `FechaAnulado` tiene valor |
| `Usuario.Rol` (CHECK Admin/User) y `UX_Usuario_Email` | Roles en el token; el login es por email |
| `PedidoDetalle.Eliminado` NOT NULL DEFAULT 0 | Baja lógica de líneas al editar |

La eliminación lógica usa las columnas que ya existían (`FechaAnulado`, `IDUsuarioAnulado`), más `Estado = 'Anulado'`. Un filtro global de EF oculta los anulados en todas las consultas.

**Scripts** en `database/`:

- `00_crear_base_datos.sql`: crea la base si no existe.
- `01_esquema_y_datos_idempotente.sql`: generado con `dotnet ef migrations script --idempotent`. Equivale a las migraciones y se puede ejecutar varias veces. Se probó sobre una base vacía y sobre una con las tablas del modelo ya creadas. Ejecutar con `-I -f 65001`, para mantener las tildes y `QUOTED_IDENTIFIER ON`.

---

## 3. Reglas de negocio

| Regla | Dónde se aplica | Respuesta |
|---|---|---|
| Total del pedido > 0 | Dominio (`Pedido.RecalcularTotales`) + CHECK en BD | 422 `PEDIDO_TOTAL_INVALIDO` |
| Número de pedido único (incluidos los anulados) | Servicio + índice único; la carrera entre dos altas se traduce en `UnitOfWork` | 409 `PEDIDO_NUMERO_DUPLICADO` |
| Solo usuarios autenticados | `FallbackPolicy` global (seguro por defecto) + `[Authorize]` | 401 |
| Eliminación lógica | `Pedido.Anular()`: estado `Anulado` y fecha/usuario de anulación | 204 |
| Eliminar solo con rol Admin | Política `SoloAdmin` | 403 `ROL_INSUFICIENTE` |
| El estado solo avanza (Registrado → Confirmado → Despachado → Entregado) | Dominio | 422 `ESTADO_RETROCESO` |
| Un pedido entregado no se modifica ni se elimina | Dominio | 422 `PEDIDO_ENTREGADO` |
| Edición concurrente | `rowVersion` en el PUT + token de concurrencia en EF | 409 `PEDIDO_MODIFICADO` |
| Cliente, sucursal y productos deben ser de la empresa y estar activos | Servicio | 422 |
| Descuento de línea ≤ subtotal; cantidad > 0 | Validador + dominio | 400 / 422 |

### Mantenedores de clientes y productos

| Regla | Respuesta |
|---|---|
| El documento del cliente se valida según su tipo: DNI de 8 dígitos; RUC de 11 dígitos que empieza con 10, 15, 17 o 20 | 400 en `numeroDocumento` |
| Un RUC exige razón social; los demás documentos exigen nombre y apellido paterno. Solo se guarda el juego de nombres que corresponde, así el nombre visible no mezcla datos | 400 por campo |
| Documento único por empresa y tipo; código de producto único por empresa (se guarda en mayúsculas) | 409 |
| Cliente o producto **inactivo**: sigue existiendo, pero no se ofrece en pedidos nuevos | — |
| **Eliminar** es una baja lógica, solo para Admin. Los pedidos históricos conservan la referencia | 204 / 403 |
| No se elimina un cliente o producto con pedidos **en curso** (Registrado, Confirmado o Despachado) | 422 |
| Editar un pedido cuyo cliente o producto se desactivó después **no se bloquea**: solo se validan las referencias nuevas | — |

Cliente y Producto **no** tienen filtro global de eliminados, a diferencia de Pedido. Pedido los referencia como navegación requerida, y EF convertiría el filtro en un INNER JOIN que haría desaparecer los pedidos históricos de un cliente dado de baja.

La migración `MantenedoresClienteProducto` agrega `Producto.Eliminado`. Producto solo tenía `Estado`, y hacía falta distinguir "desactivado" de "eliminado". También crea índices de búsqueda por documento y por código. Las demás columnas (dirección, celular, auditoría, unidad de medida) ya existían en el modelo referencial.

El total **lo calcula el servidor** a partir de las líneas (`PedidoDetalle`); el cliente no lo envía. Es la misma regla que el modelo referencial (cabecera + detalle), y evita un total que no cuadre con sus líneas.

---

## 4. API

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/auth/login` | Anónimo (máx. 10/min por IP) | Devuelve `{ token, expiresIn, tokenType, usuario }` |
| GET | `/api/pedidos` | Token | Lista paginada. Query: `buscar`, `estado`, `desde`, `hasta`, `ordenarPor` (numero, cliente, fecha, total, estado), `descendente`, `page`, `pageSize` |
| GET | `/api/pedidos/{id}` | Token | Pedido con sus líneas y `rowVersion` |
| GET | `/api/pedidos/siguiente-numero` | Token | Sugerencia `PED-###` |
| POST | `/api/pedidos` | Token | Crea; responde 201 con `Location` |
| PUT | `/api/pedidos/{id}` | Token | Actualiza cabecera, estado y líneas (las líneas llevan `id` si ya existían) |
| DELETE | `/api/pedidos/{id}` | Admin | Eliminación lógica |
| GET | `/api/pedidos/resumen` | Token | Cantidad de pedidos vigentes por estado |
| GET | `/api/clientes` · `/api/productos` | Token | Lista paginada. Query: `buscar`, `estado` (activos, inactivos), `page`, `pageSize` |
| GET / POST / PUT | `/api/clientes/{id}` · `/api/productos/{id}` | Token | Obtener, crear y editar |
| DELETE | `/api/clientes/{id}` · `/api/productos/{id}` | Admin | Baja lógica |
| GET | `/api/catalogos/{clientes\|productos\|monedas\|sucursales\|tipos-documento\|unidades-medida}` | Token | Datos para los combos (solo activos) |
| GET | `/health` | Anónimo | Estado de la API y de SQL Server |

Ejemplo de creación:

```json
POST /api/pedidos
{
  "numeroPedido": "PED-008",
  "clienteId": 1,
  "sucursalId": 1,
  "monedaId": 1,
  "fecha": "2025-01-10",
  "detalles": [
    { "productoId": 4, "cantidad": 1, "precioVenta": 250.75, "descuento": 0 }
  ]
}
```

Respuesta (resumida): `{ "id": 8, "numeroPedido": "PED-008", "cliente": "Juan Perez", "fecha": "2025-01-10", "total": 250.75, "estado": "Registrado", "rowVersion": "AAAAAAAAB9M=", "detalles": [...] }`

Formato de error (todos los 4xx y 5xx):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Datos inválidos",
  "status": 400,
  "detail": "Revise los campos marcados.",
  "code": "VALIDACION",
  "errors": { "detalles[0].cantidad": ["La cantidad debe ser mayor a 0."] },
  "traceId": "00-5249e125a825d65812bc6d2f721384a1-039a4721a4f92730-00"
}
```

---

## 5. Seguridad

- **JWT Bearer** firmado con HMAC-SHA256 y expiración de 60 minutos. Claims: `sub`, `email`, `name`, `role` y `empresa_id`. Se validan emisor, audiencia, firma, algoritmo y vigencia, con 30 s de tolerancia de reloj.
- **La clave de firma no está en `appsettings.json`**, solo en `appsettings.Development.json`. En otro entorno, si falta `Jwt__SigningKey` o tiene menos de 32 caracteres, la API **se niega a arrancar** (`ValidateOnStart`).
- **Seguro por defecto:** la `FallbackPolicy` exige autenticación en todo endpoint que no declare `[AllowAnonymous]`.
- **401 y 403 con cuerpo:** los eventos `OnChallenge` y `OnForbidden` devuelven ProblemDetails y distinguen `TOKEN_EXPIRADO` de `TOKEN_REQUERIDO`, para que el front sepa cuándo la sesión venció.
- **Contraseñas con BCrypt** (work factor 11).
- **Protección contra fuerza bruta en dos niveles:**
  - Rate limit de 10 logins por minuto por IP (429).
  - Bloqueo de la cuenta tras 5 intentos fallidos durante 15 minutos. Usa las columnas `NumIntentoFallidoLogin`, `Bloqueado` y `FechaBloqueo` del modelo.
- **Rate limiting global:** token bucket por usuario autenticado (o por IP), 300 peticiones/min con ráfagas. Excluye `/health` y `/swagger`. Responde 429 con `Retry-After`.
- **Control de sesión en cada request** (`OnTokenValidated`): un JWT firmado y vigente no basta, también se contrasta con el estado actual de la cuenta. Se responde **401 `SESION_REVOCADA`** si:
  - el administrador deshabilitó el acceso,
  - la cuenta está bloqueada,
  - el rol cambió,
  - o la clave se restableció después de emitido el token (se compara con `FechaUltimoCambiarClave` al milisegundo, con el claim `iat_ms`).

  El estado se cachea 30 s y cada cambio del administrador invalida la caché, así el efecto es inmediato. Si la BD no responde, el control se omite (firma y vigencia ya se validaron): una caída de la base no expulsa a todos los usuarios con un 401, sino que la operación responde 503.
- **Sin enumeración de usuarios:** un email inexistente responde igual que una clave incorrecta, y también ejecuta un `Verify` BCrypt contra un hash señuelo, para que el tiempo de respuesta no delate qué correos existen.
- **HTTPS:**
  - La API redirige HTTP → HTTPS en todo entorno con puerto HTTPS; HSTS fuera de Development.
  - En desarrollo el front también se sirve por HTTPS, con el certificado de ASP.NET exportado por `npm run certs`.
- **Headers:** `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` y CSP en la API. Se quita el header `Server`.
- **CORS** restringido a los orígenes configurados.

### Panel de Administración (solo Admin)

| Acción | Endpoint | Reglas |
|---|---|---|
| Listar usuarios | `GET /api/admin/usuarios?buscar=` | De la empresa del administrador |
| Crear usuario | `POST /api/admin/usuarios` | Email y usuario únicos (409). Clave de 8 a 72 caracteres con mayúscula, minúscula y número |
| Editar datos, rol y acceso | `PUT /api/admin/usuarios/{id}` | No puede quitarse su propio rol ni deshabilitarse (422). La empresa conserva al menos un Admin activo (422). Cierra las sesiones del usuario |
| Desbloquear | `POST /api/admin/usuarios/{id}/desbloquear` | Reinicia los intentos fallidos |
| Restablecer clave | `POST /api/admin/usuarios/{id}/restablecer-clave` | Desbloquea y **revoca los tokens emitidos antes** |
| Estado del sistema | `GET /api/admin/sistema` | Salud de SQL Server, estado del circuit breaker, tiempo activo y parámetros vigentes de seguridad y resiliencia (nunca secretos) |

`IDUsuario` no es identity en el modelo referencial. El alta usa MAX+1, aceptable para una operación manual de administrador; si dos altas simultáneas chocan, la PK lo impide y la API responde 409.

## 6. Resiliencia y observabilidad

### Pipeline de resiliencia de SQL Server (Polly v8)

Todo acceso a la base (consultas, `SaveChanges`, migraciones y el health check) pasa por **un único pipeline**, porque está conectado como estrategia de ejecución de EF (`PollyExecutionStrategy`). Ningún repositorio tiene que acordarse de usarlo.

```
Petición ─▶ Retry ─▶ Circuit breaker ─▶ Timeout ─▶ SQL Server
```

| Estrategia | Configuración (`Resilience:Sql`) | Se activa con |
|---|---|---|
| **Retry** | 3 reintentos, backoff exponencial desde 200 ms con jitter | Errores **transitorios**: timeout (-2), deadlock (1205), lock timeout (1222), red (20, 64, 233, 10053, 10054, 10060), throttling y failover de Azure SQL |
| **Circuit breaker** | Se abre con ≥ 50 % de fallos en 60 s (mínimo 5 ejecuciones); queda abierto 20 s y luego pasa a semiabierto | Transitorios **y** errores de conexión (servidor inexistente, conexión rechazada 10061, BD inaccesible) |
| **Timeout** | 30 s por intento | Intentos colgados |

- **Singleton:** el estado del circuito es compartido por todas las peticiones y refleja la salud real de la BD.
- **Estrategias anidadas:** EF anida estrategias de ejecución, y solo el nivel externo pasa por el pipeline. Si no, los reintentos se multiplicarían y el circuito contaría dos veces el mismo fallo.
- **Transacciones explícitas:** dentro de una transacción no se reintenta, porque repetir solo una parte no es seguro.
- **Circuito abierto:** **503 `CIRCUITO_ABIERTO` con `Retry-After`** inmediato, sin tocar la BD.
- **Base caída sin circuito abierto:** **503 `BD_NO_DISPONIBLE`**. Nunca un 500 genérico.
- **Estado visible** en `/health` (chequeo `circuito-sql`) y en el Panel de Administración.

**Prueba en vivo.** Se levantó una segunda instancia de la API apuntando a un SQL Server inexistente:

| Peticiones | Respuesta | Tiempo |
|---|---|---|
| 1–2 | 503 `BD_NO_DISPONIBLE` | ~16 s cada una: la conexión rechazada tarda en fallar |
| 3 | 503 `CIRCUITO_ABIERTO` | Se abre el circuito |
| 4 en adelante | 503 `CIRCUITO_ABIERTO`, `Retry-After: 20` | ~7 ms: no se toca la BD |

Esa prueba también mostró un ajuste necesario: con un mínimo de 8 ejecuciones en 30 s, el circuito nunca se abría cuando cada fallo es lento. Por eso el valor quedó en 5 ejecuciones en 60 s. Los tests unitarios de `SqlResilienceTests` cubren reintento, no reintento de errores de negocio, apertura del circuito y fallo rápido.
- **Concurrencia optimista** con `rowversion`: sin bloqueos pesimistas y sin "gana el último".
- **Carrera en el número único:** si dos altas simultáneas pasan la validación previa, el índice único detiene a la segunda y `UnitOfWork` la traduce a 409, nunca a 500.
- **`CancellationToken`** propagado desde el request hasta EF: si el cliente cancela, la consulta se aborta.
- **Logging estructurado con Serilog:**
  - Consola legible, y archivo JSON compacto diario (`logs/api-AAAAMMDD.json`, 14 días).
  - Cada línea lleva `CorrelationId`: se reutiliza el header `X-Correlation-ID` o se genera uno, y se devuelve en la respuesta.
  - Cada request registra usuario, IP, status y duración.
  - Los 4xx se registran como Information y los 5xx como Error con stack.
- **Health check** en `/health`: conectividad con SQL Server y estado del circuit breaker.
- **Logs de Polly** (reintentos y cambios de estado del circuito) en el mismo log estructurado. Con el circuito abierto, los 503 se registran como Warning sin stack trace, para no inundar el log.

---

## 7. Frontend

```
frontend/src/
├─ api/          http.ts (axios + interceptores → ApiError), services.ts (endpoints tipados)
├─ auth/         AuthContext (sesión, expiración), ProtectedRoute
├─ components/   ui (Button, Field, Drawer, Switch, Segmented, Paginacion, ConfirmDialog…), Toast, ClienteCombobox
├─ features/     clientes/ y productos/: listado + formulario en panel lateral; admin/: panel de administración
├─ hooks/        useListadoMaestro (filtros en URL, debounce, carga cancelable), useDebounce
├─ layout/       AppLayout (menú lateral, usuario, tiempo de sesión)
├─ pages/        LoginPage, PedidosListPage, PedidoFormPage (crear/editar)
└─ router.tsx    rutas públicas y protegidas
```

**Pantallas:**
- Login.
- Pedidos: listado, crear, editar y eliminar.
- Clientes y Productos: listado, crear, editar y eliminar.
- **Panel de Administración**, solo Admin:
  - **Usuarios y accesos:** alta con generador de clave segura, rol, habilitar/deshabilitar, desbloquear y restablecer clave.
  - **Estado del sistema:** salud de la BD, circuit breaker y parámetros vigentes, con refresco cada 10 s.

El menú lateral tiene las secciones **Operaciones**, **Maestros** y **Administración** (esta última solo para Admin); en móvil se abre como cajón. Si el administrador revoca una sesión, el usuario vuelve al login y ve el motivo.

**Identidad visual:**
- **Primario `#0029E0`:** acciones de guardar, enlaces, foco y filtro activo.
- **Secundario `#E04300`:** botones de crear, indicador del menú activo, alertas de sesión y detalles de marca.
- Ambos se definen como escalas `brand-*` y `accent-*` en el `@theme` de Tailwind (`src/index.css`); cambiar la paleta es tocar un solo archivo.
- Tipografía Inter e isotipo propio (favicon incluido).

**Sesión:**
- El token se guarda en `sessionStorage`: sobrevive a un F5 pero no queda en el equipo al cerrar la pestaña.
- La sesión se cierra sola al vencer el `exp` del JWT, o ante cualquier 401. En ese caso el login muestra el motivo y, al volver a entrar, regresa a la pantalla donde estaba el usuario.

### Mejoras de UX (bonus)

- **Tarjetas de resumen por estado** en el listado de pedidos; un clic filtra la tabla.
- **Alta de cliente sin salir del pedido:** "¿Cliente nuevo? Regístralo aquí" abre el panel lateral y deja al cliente seleccionado.
- **Mantenedores en panel lateral:** se edita sin perder la posición en la lista. El formulario de cliente cambia de campos según DNI o RUC y solo acepta dígitos en el documento.
- La papelera de clientes y productos aparece deshabilitada si hay pedidos en curso, con el motivo en el tooltip.
- **Filtros en la URL** (búsqueda, estado, fechas, orden y página): se pueden compartir, sobreviven a F5 y el botón Atrás los respeta.
- Búsqueda con debounce por número, cliente o documento; orden por columna; paginación con tamaño configurable.
- **Número de pedido sugerido** (`PED-###`) y editable. Si choca, se ofrece "Usar PED-00X" con un clic.
- **Combobox de clientes** con búsqueda sin tildes y navegación por teclado (patrón ARIA).
- **Totales en vivo** mientras se editan las líneas; el total ≤ 0 se marca antes de enviar.
- **Validación en el cliente** con las mismas reglas del backend. Los errores del servidor se pintan en el campo exacto, incluidos los de cada línea (`detalles[2].cantidad`).
- **Aviso de cambios sin guardar**, tanto al navegar dentro de la app como al cerrar la pestaña.
- **Ctrl+S** para guardar.
- **Conflicto de edición concurrente:** avisa que otro usuario modificó el pedido y ofrece recargar los datos.
- **Cuenta regresiva de la sesión** en la barra, con aviso cuando quedan 5 minutos.
- **La UI refleja los permisos:**
  - Eliminar aparece deshabilitado para el rol User, con el motivo en el tooltip.
  - Un pedido entregado se abre en modo consulta.
  - El selector de estado solo ofrece estados hacia adelante.
- Toasts, esqueletos de carga, estados vacíos con acción, fila resaltada tras guardar, y vista en tarjetas en móvil.

---

## 8. Qué haría con más tiempo

- **Pruebas de integración** con `WebApplicationFactory` + Testcontainers (SQL Server real), y pruebas de componentes en el front (Vitest + Testing Library).
- **Refresh token** con rotación en cookie `httpOnly`. `sessionStorage` es expuesto a XSS; es un compromiso aceptable para el reto, no para producción.
- **Historial de cambios de estado del pedido**, y precio de lista en el producto: hoy el precio se ingresa en cada línea porque el modelo referencial no tiene esa columna.
- **Secretos** en un gestor (Key Vault o user-secrets) en lugar de `appsettings.Development.json`.
