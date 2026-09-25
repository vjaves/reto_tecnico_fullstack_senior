using Atlantic.Pedidos.Domain.Common;

namespace Atlantic.Pedidos.Domain.Usuarios;

public class Usuario
{
    private Usuario() { }

    public static Usuario Crear(int id, int empresaId, string nombreUsuario, string numeroDocumento, string email,
        string nombreCompleto, string rol, string claveHash, int? usuarioCreadorId = null, DateTime? ahora = null)
    {
        ValidarRol(rol);

        return new Usuario
        {
            Id = id,
            EmpresaId = empresaId,
            NombreUsuario = nombreUsuario.Trim(),
            NumeroDocumento = numeroDocumento.Trim(),
            Email = NormalizarEmail(email),
            NombreCompleto = nombreCompleto.Trim(),
            Rol = rol,
            ClaveHash = claveHash,
            Eliminado = false,
            NumIntentoFallidoLogin = 0,
            UsuarioCreacionId = usuarioCreadorId,
            FechaCreacion = ahora,
            FechaUltimoCambiarClave = ahora
        };
    }

    public int Id { get; private set; }
    public int? EmpresaId { get; private set; }
    public string NombreUsuario { get; private set; } = string.Empty;
    public string NumeroDocumento { get; private set; } = string.Empty;
    public string ClaveHash { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Rol { get; private set; } = string.Empty;
    public bool Bloqueado { get; private set; }
    public DateTime? FechaBloqueo { get; private set; }
    public int? NumIntentoFallidoLogin { get; private set; }
    public DateTime? FechaUltimoIntentoLogin { get; private set; }
    public DateTime? FechaUltimoLogin { get; private set; }

    /// <summary>
    /// Momento del último cambio de clave. Los tokens emitidos antes quedan revocados: restablecer la
    /// clave de una cuenta comprometida cierra también las sesiones abiertas.
    /// </summary>
    public DateTime? FechaUltimoCambiarClave { get; private set; }

    /// <summary>En el modelo referencial "Eliminado" es el acceso deshabilitado: el usuario no puede ingresar.</summary>
    public bool? Eliminado { get; private set; }

    public int? UsuarioCreacionId { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public int? UsuarioModificacionId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }

    public bool EstaActivo => Eliminado != true;

    /// <summary>
    /// El bloqueo por intentos fallidos es temporal: vence solo pasado <paramref name="duracion"/>.
    /// </summary>
    public bool EstaBloqueado(DateTime ahora, TimeSpan duracion) =>
        Bloqueado && (FechaBloqueo is null || FechaBloqueo.Value.Add(duracion) > ahora);

    public void RegistrarLoginFallido(DateTime ahora, int maxIntentos, TimeSpan duracionBloqueo)
    {
        // Si el bloqueo anterior ya venció, el conteo empieza de nuevo; si no, un solo
        // error tras el desbloqueo volvería a bloquear la cuenta.
        if (Bloqueado && !EstaBloqueado(ahora, duracionBloqueo))
        {
            Bloqueado = false;
            FechaBloqueo = null;
            NumIntentoFallidoLogin = 0;
        }

        NumIntentoFallidoLogin = (NumIntentoFallidoLogin ?? 0) + 1;
        FechaUltimoIntentoLogin = ahora;

        if (NumIntentoFallidoLogin >= maxIntentos)
        {
            Bloqueado = true;
            FechaBloqueo = ahora;
        }
    }

    public void RegistrarLoginExitoso(DateTime ahora)
    {
        NumIntentoFallidoLogin = 0;
        Bloqueado = false;
        FechaBloqueo = null;
        FechaUltimoIntentoLogin = ahora;
        FechaUltimoLogin = ahora;
    }

    public void Actualizar(string nombreCompleto, string numeroDocumento, string email, string rol, bool activo,
        int usuarioId, DateTime ahora)
    {
        ValidarRol(rol);
        NombreCompleto = nombreCompleto.Trim();
        NumeroDocumento = numeroDocumento.Trim();
        Email = NormalizarEmail(email);
        Rol = rol;
        Eliminado = !activo;
        Modificado(usuarioId, ahora);
    }

    public void Desbloquear(int usuarioId, DateTime ahora)
    {
        if (!Bloqueado)
            throw new BusinessRuleException("USUARIO_NO_BLOQUEADO", "El usuario no está bloqueado.");

        Bloqueado = false;
        FechaBloqueo = null;
        NumIntentoFallidoLogin = 0;
        Modificado(usuarioId, ahora);
    }

    /// <summary>Nueva clave: desbloquea la cuenta y revoca los tokens emitidos antes de este momento.</summary>
    public void CambiarClave(string nuevoHash, int usuarioId, DateTime ahora)
    {
        ClaveHash = nuevoHash;
        FechaUltimoCambiarClave = ahora;
        Bloqueado = false;
        FechaBloqueo = null;
        NumIntentoFallidoLogin = 0;
        Modificado(usuarioId, ahora);
    }

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    private void Modificado(int usuarioId, DateTime ahora)
    {
        UsuarioModificacionId = usuarioId;
        FechaModificacion = ahora;
    }

    private static void ValidarRol(string rol)
    {
        if (!Roles.Todos.Contains(rol))
            throw new BusinessRuleException("ROL_INVALIDO", $"Rol no válido: {rol}.");
    }
}
