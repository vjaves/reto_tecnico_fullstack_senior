using Atlantic.Pedidos.Api.Security;
using Atlantic.Pedidos.Application.Clientes;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Productos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlantic.Pedidos.Api.Controllers;

/// <summary>Mantenedor de clientes. Crear y editar: cualquier usuario; eliminar (baja lógica): Admin.</summary>
[ApiController]
[Route("api/clientes")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ClientesController(IClienteService clientes) : ControllerBase
{
    /// <summary>Lista paginada. <c>estado</c>: activos | inactivos (vacío = todos).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClienteResumenDto>>> Listar([FromQuery] MaestroFiltro filtro, CancellationToken ct) =>
        Ok(await clientes.ListarAsync(filtro, ct));

    [HttpGet("{id:int}", Name = "ObtenerCliente")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDetalleDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await clientes.ObtenerAsync(id, ct));

    /// <response code="400">Datos inválidos, incluido un documento que no cumple el formato de su tipo (DNI 8, RUC 11 dígitos).</response>
    /// <response code="409">Ya existe un cliente con ese documento.</response>
    [HttpPost]
    [ProducesResponseType<ClienteDetalleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteDetalleDto>> Crear(GuardarClienteRequest request, CancellationToken ct)
    {
        var creado = await clientes.CrearAsync(request, ct);
        return CreatedAtRoute("ObtenerCliente", new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteDetalleDto>> Actualizar(int id, GuardarClienteRequest request, CancellationToken ct) =>
        Ok(await clientes.ActualizarAsync(id, request, ct));

    /// <response code="422">El cliente tiene pedidos en curso.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.SoloAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await clientes.EliminarAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Mantenedor de productos. Crear y editar: cualquier usuario; eliminar (baja lógica): Admin.</summary>
[ApiController]
[Route("api/productos")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ProductosController(IProductoService productos) : ControllerBase
{
    /// <summary>Lista paginada. <c>estado</c>: activos | inactivos (vacío = todos).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductoResumenDto>>> Listar([FromQuery] MaestroFiltro filtro, CancellationToken ct) =>
        Ok(await productos.ListarAsync(filtro, ct));

    [HttpGet("{id:int}", Name = "ObtenerProducto")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDetalleDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await productos.ObtenerAsync(id, ct));

    /// <response code="409">Ya existe un producto con ese código.</response>
    [HttpPost]
    [ProducesResponseType<ProductoDetalleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoDetalleDto>> Crear(GuardarProductoRequest request, CancellationToken ct)
    {
        var creado = await productos.CrearAsync(request, ct);
        return CreatedAtRoute("ObtenerProducto", new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoDetalleDto>> Actualizar(int id, GuardarProductoRequest request, CancellationToken ct) =>
        Ok(await productos.ActualizarAsync(id, request, ct));

    /// <response code="422">El producto está en pedidos en curso.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.SoloAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await productos.EliminarAsync(id, ct);
        return NoContent();
    }
}
