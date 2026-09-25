using Atlantic.Pedidos.Application.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlantic.Pedidos.Api.Controllers;

/// <summary>Datos para los combos del formulario de pedido, acotados a la empresa del usuario.</summary>
[ApiController]
[Route("api/catalogos")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogosController(ICatalogoService catalogos) : ControllerBase
{
    [HttpGet("clientes")]
    public async Task<ActionResult<IReadOnlyList<ClienteDto>>> Clientes(CancellationToken ct) =>
        Ok(await catalogos.ClientesAsync(ct));

    [HttpGet("productos")]
    public async Task<ActionResult<IReadOnlyList<ProductoDto>>> Productos(CancellationToken ct) =>
        Ok(await catalogos.ProductosAsync(ct));

    [HttpGet("monedas")]
    public async Task<ActionResult<IReadOnlyList<ItemCatalogoDto>>> Monedas(CancellationToken ct) =>
        Ok(await catalogos.MonedasAsync(ct));

    [HttpGet("sucursales")]
    public async Task<ActionResult<IReadOnlyList<ItemCatalogoDto>>> Sucursales(CancellationToken ct) =>
        Ok(await catalogos.SucursalesAsync(ct));

    [HttpGet("tipos-documento")]
    public async Task<ActionResult<IReadOnlyList<ItemCatalogoDto>>> TiposDocumento(CancellationToken ct) =>
        Ok(await catalogos.TiposDocumentoAsync(ct));

    [HttpGet("unidades-medida")]
    public async Task<ActionResult<IReadOnlyList<ItemCatalogoDto>>> UnidadesMedida(CancellationToken ct) =>
        Ok(await catalogos.UnidadesMedidaAsync(ct));
}
