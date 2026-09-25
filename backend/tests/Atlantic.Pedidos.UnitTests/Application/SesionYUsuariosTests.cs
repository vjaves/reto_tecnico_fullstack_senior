using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Application.Usuarios;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Usuarios;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Atlantic.Pedidos.UnitTests.Application;

public class ValidadorSesionTests
{
    private readonly IEstadoSesionProvider _sesiones = Substitute.For<IEstadoSesionProvider>();
    private readonly ValidadorSesion _sut;
    private static readonly DateTime EmitidoUtc = DateTime.UtcNow.AddMinutes(-10);

    public ValidadorSesionTests() =>
        _sut = new ValidadorSesion(_sesiones, TimeProvider.System, Options.Create(new LockoutOptions()));

    private void Estado(bool activo = true, bool bloqueado = false, string rol = Roles.User, DateTime? cambioClave = null) =>
        _sesiones.ObtenerAsync(1, Arg.Any<CancellationToken>())
            .Returns(new EstadoSesion(activo, bloqueado, bloqueado ? DateTime.Now : null, rol, 1, cambioClave));

    [Fact]
    public async Task Cuenta_sin_cambios_mantiene_la_sesion()
    {
        Estado();
        (await _sut.ValidarAsync(1, 1, Roles.User, EmitidoUtc, default)).Should().BeNull();
    }

    [Fact]
    public async Task Cuenta_desactivada_revoca_la_sesion()
    {
        Estado(activo: false);
        (await _sut.ValidarAsync(1, 1, Roles.User, EmitidoUtc, default)).Should().Contain("deshabilitado");
    }

    [Fact]
    public async Task Cambio_de_rol_revoca_la_sesion()
    {
        Estado(rol: Roles.Admin);
        (await _sut.ValidarAsync(1, 1, Roles.User, EmitidoUtc, default)).Should().Contain("permisos");
    }

    [Fact]
    public async Task Clave_restablecida_despues_del_token_revoca_la_sesion()
    {
        Estado(cambioClave: DateTime.Now.AddMinutes(-1));
        (await _sut.ValidarAsync(1, 1, Roles.User, EmitidoUtc, default)).Should().Contain("contraseña");
    }

    [Fact]
    public async Task Token_emitido_despues_del_cambio_de_clave_es_valido()
    {
        Estado(cambioClave: DateTime.Now.AddMinutes(-30));
        (await _sut.ValidarAsync(1, 1, Roles.User, EmitidoUtc, default)).Should().BeNull();
    }
}

public class UsuarioAdminServiceTests
{
    private readonly IUsuarioRepository _repo = Substitute.For<IUsuarioRepository>();
    private readonly IEstadoSesionProvider _sesiones = Substitute.For<IEstadoSesionProvider>();
    private readonly ICurrentUser _admin = Substitute.For<ICurrentUser>();
    private readonly UsuarioAdminService _sut;

    public UsuarioAdminServiceTests()
    {
        _admin.UsuarioId.Returns(1);
        _admin.EmpresaId.Returns(1);
        var consultas = Substitute.For<IUsuarioQueries>();
        consultas.ListarAsync(1, null, Arg.Any<CancellationToken>())
            .Returns(ci => new List<UsuarioAdminDto> { new(1, "admin", "Admin", "1", "a@x.com", Roles.Admin, true, false, 0, null, null),
                                                       new(2, "user", "User", "2", "u@x.com", Roles.User, true, false, 0, null, null) });
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");

        _sut = new UsuarioAdminService(_repo, consultas, _sesiones, hasher, Substitute.For<IUnitOfWork>(), _admin,
            TimeProvider.System, new CrearUsuarioRequestValidator(), new ActualizarUsuarioRequestValidator(),
            new RestablecerClaveRequestValidator(), NullLogger<UsuarioAdminService>.Instance);
    }

    private static Usuario Usuario(int id, string rol) =>
        Atlantic.Pedidos.Domain.Usuarios.Usuario.Crear(id, 1, $"u{id}", "12345678", $"u{id}@x.com", $"Usuario {id}", rol, "hash");

    [Fact]
    public async Task Un_admin_no_puede_quitarse_su_propio_rol()
    {
        _repo.ObtenerAsync(1, 1, Arg.Any<CancellationToken>()).Returns(Usuario(1, Roles.Admin));

        var accion = () => _sut.ActualizarAsync(1, new ActualizarUsuarioRequest
        {
            NombreCompleto = "Admin", NumeroDocumento = "1", Email = "a@x.com", Rol = Roles.User, Activo = true
        }, default);

        (await accion.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("USUARIO_AUTOGESTION");
    }

    [Fact]
    public async Task No_se_puede_dejar_a_la_empresa_sin_administradores()
    {
        _repo.ObtenerAsync(3, 1, Arg.Any<CancellationToken>()).Returns(Usuario(3, Roles.Admin));
        _repo.ContarAdminsActivosAsync(1, 3, Arg.Any<CancellationToken>()).Returns(0);

        var accion = () => _sut.ActualizarAsync(3, new ActualizarUsuarioRequest
        {
            NombreCompleto = "Otro", NumeroDocumento = "3", Email = "u3@x.com", Rol = Roles.Admin, Activo = false
        }, default);

        (await accion.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("ULTIMO_ADMIN");
    }

    [Fact]
    public async Task Restablecer_la_clave_invalida_la_sesion_en_cache()
    {
        var usuario = Usuario(2, Roles.User);
        _repo.ObtenerAsync(2, 1, Arg.Any<CancellationToken>()).Returns(usuario);

        await _sut.RestablecerClaveAsync(2, new RestablecerClaveRequest("NuevaClave1"), default);

        usuario.FechaUltimoCambiarClave.Should().NotBeNull();
        _sesiones.Received(1).Invalidar(2);
    }

    [Theory]
    [InlineData("corta1A")]
    [InlineData("sinmayuscula1")]
    [InlineData("SINMINUSCULA1")]
    [InlineData("SinNumeroAqui")]
    public async Task La_clave_debe_ser_segura(string clave)
    {
        var accion = () => _sut.RestablecerClaveAsync(2, new RestablecerClaveRequest(clave), default);
        await accion.Should().ThrowAsync<ValidationException>();
    }
}
