using Atlantic.Pedidos.Api.Security;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Pedidos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlantic.Pedidos.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class PedidosController(IPedidoService pedidos) : ControllerBase
{
    /// <summary>Lista paginada con búsqueda (número, cliente o documento), estado, rango de fechas y orden.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PedidoResumenDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PedidoResumenDto>>> Listar([FromQuery] PedidoFiltro filtro, CancellationToken ct) =>
        Ok(await pedidos.ListarAsync(filtro, ct));

    /// <summary>Sugerencia del próximo número correlativo (PED-###).</summary>
    [HttpGet("siguiente-numero")]
    [ProducesResponseType<SiguienteNumeroDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiguienteNumeroDto>> SiguienteNumero(CancellationToken ct) =>
        Ok(await pedidos.SiguienteNumeroAsync(ct));

    /// <summary>Cantidad de pedidos vigentes por estado (tarjetas del listado).</summary>
    [HttpGet("resumen")]
    [ProducesResponseType<PedidoResumenEstadosDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PedidoResumenEstadosDto>> Resumen(CancellationToken ct) =>
        Ok(await pedidos.ResumenAsync(ct));

    [HttpGet("{id:int}", Name = nameof(Obtener))]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await pedidos.ObtenerAsync(id, ct));

    /// <response code="201">Pedido creado.</response>
    /// <response code="400">Datos con formato inválido.</response>
    /// <response code="409">El número de pedido ya existe.</response>
    /// <response code="422">Regla de negocio incumplida (p. ej. total menor o igual a 0).</response>
    [HttpPost]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> Crear(CrearPedidoRequest request, CancellationToken ct)
    {
        var creado = await pedidos.CrearAsync(request, ct);
        return CreatedAtRoute(nameof(Obtener), new { id = creado.Id }, creado);
    }

    /// <response code="200">Pedido actualizado; incluye el nuevo rowVersion.</response>
    /// <response code="409">Número duplicado, o el pedido cambió desde que se leyó (rowVersion).</response>
    /// <response code="422">Regla de negocio incumplida (total, transición de estado, pedido entregado).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> Actualizar(int id, ActualizarPedidoRequest request, CancellationToken ct) =>
        Ok(await pedidos.ActualizarAsync(id, request, ct));

    /// <summary>Eliminación lógica (el pedido pasa a Anulado). Sólo rol Admin.</summary>
    /// <response code="204">Pedido anulado.</response>
    /// <response code="403">El usuario no tiene rol Admin.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.SoloAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await pedidos.EliminarAsync(id, ct);
        return NoContent();
    }
}
