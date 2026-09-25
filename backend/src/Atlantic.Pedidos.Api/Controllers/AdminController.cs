using Atlantic.Pedidos.Api.Admin;
using Atlantic.Pedidos.Api.Security;
using Atlantic.Pedidos.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlantic.Pedidos.Api.Controllers;

/// <summary>Panel de administración: gestión de usuarios y estado del sistema. Sólo rol Admin.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthPolicies.SoloAdmin)]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AdminController(IUsuarioAdminService usuarios, EstadoSistemaService sistema) : ControllerBase
{
    /// <summary>Usuarios de la empresa, con su estado de acceso (activo, bloqueado, intentos fallidos).</summary>
    [HttpGet("usuarios")]
    public async Task<ActionResult<IReadOnlyList<UsuarioAdminDto>>> Usuarios([FromQuery] string? buscar, CancellationToken ct) =>
        Ok(await usuarios.ListarAsync(buscar, ct));

    /// <response code="409">El email o el nombre de usuario ya existen.</response>
    [HttpPost("usuarios")]
    [ProducesResponseType<UsuarioAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioAdminDto>> CrearUsuario(CrearUsuarioRequest request, CancellationToken ct)
    {
        var creado = await usuarios.CrearAsync(request, ct);
        return Created($"/api/admin/usuarios/{creado.Id}", creado);
    }

    /// <summary>Datos, rol y acceso. Un cambio de rol o la desactivación cierran las sesiones abiertas del usuario.</summary>
    /// <response code="422">Autogestión (quitarse Admin o desactivarse) o se quedaría la empresa sin administrador.</response>
    [HttpPut("usuarios/{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioAdminDto>> ActualizarUsuario(int id, ActualizarUsuarioRequest request, CancellationToken ct) =>
        Ok(await usuarios.ActualizarAsync(id, request, ct));

    [HttpPost("usuarios/{id:int}/desbloquear")]
    public async Task<ActionResult<UsuarioAdminDto>> Desbloquear(int id, CancellationToken ct) =>
        Ok(await usuarios.DesbloquearAsync(id, ct));

    /// <summary>Asigna una clave nueva, desbloquea la cuenta y revoca los tokens emitidos antes.</summary>
    [HttpPost("usuarios/{id:int}/restablecer-clave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RestablecerClave(int id, RestablecerClaveRequest request, CancellationToken ct)
    {
        await usuarios.RestablecerClaveAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Salud de la BD, estado del circuit breaker y parámetros de seguridad y resiliencia vigentes.</summary>
    [HttpGet("sistema")]
    public async Task<ActionResult<EstadoSistemaDto>> Sistema(CancellationToken ct) =>
        Ok(await sistema.ObtenerAsync(ct));
}
