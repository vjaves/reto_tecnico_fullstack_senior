using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Atlantic.Pedidos.Api.Security;

/// <summary>Lee la identidad desde los claims del JWT validado por el middleware.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int UsuarioId => LeerEntero(JwtRegisteredClaimNames.Sub);
    public int EmpresaId => LeerEntero(AppClaims.EmpresaId);
    public string Rol => Principal.FindFirst(AppClaims.Role)?.Value ?? string.Empty;

    private System.Security.Claims.ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new ForbiddenAccessException("No hay un usuario en el contexto.");

    private int LeerEntero(string claim) =>
        int.TryParse(Principal.FindFirst(claim)?.Value, out var valor)
            ? valor
            : throw new ForbiddenAccessException($"El token no contiene el claim '{claim}'.");
}
