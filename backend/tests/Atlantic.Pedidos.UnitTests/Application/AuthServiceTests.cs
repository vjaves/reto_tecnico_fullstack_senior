using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Usuarios;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Atlantic.Pedidos.UnitTests.Application;

public class AuthServiceTests
{
    private readonly IUsuarioRepository _usuarios = Substitute.For<IUsuarioRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokens = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly AuthService _sut;
    private readonly Usuario _usuario = Usuario.Crear(1, 1, "user", "10000002", "user@email.com", "Usuario", Roles.User, "hash");

    public AuthServiceTests()
    {
        _usuarios.ObtenerPorEmailAsync("user@email.com", Arg.Any<CancellationToken>()).Returns(_usuario);
        _hasher.Verify("123456", "hash").Returns(true);
        _tokens.Generar(_usuario).Returns(new TokenGenerado("jwt", 3600, DateTime.UtcNow.AddHours(1)));

        _sut = new AuthService(_usuarios, _hasher, _tokens, _uow, new LoginRequestValidator(), TimeProvider.System,
            Options.Create(new LockoutOptions { MaxIntentosFallidos = 3, MinutosBloqueo = 15 }),
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_correcto_devuelve_token_y_expiracion()
    {
        var respuesta = await _sut.LoginAsync(new LoginRequest("user@email.com", "123456"), CancellationToken.None);

        respuesta.Token.Should().Be("jwt");
        respuesta.ExpiresIn.Should().Be(3600);
        respuesta.Usuario.Rol.Should().Be(Roles.User);
    }

    [Fact]
    public async Task Email_inexistente_responde_lo_mismo_que_clave_incorrecta()
    {
        var accion = () => _sut.LoginAsync(new LoginRequest("nadie@email.com", "123456"), CancellationToken.None);

        (await accion.Should().ThrowAsync<AuthenticationFailedException>()).Which.Code.Should().Be("CREDENCIALES_INVALIDAS");
        // Se verifica contra un hash señuelo para no delatar por tiempo qué emails existen.
        _hasher.Received(1).Verify("123456", Arg.Any<string>());
    }

    [Fact]
    public async Task Tras_el_maximo_de_intentos_fallidos_la_cuenta_se_bloquea()
    {
        for (var i = 0; i < 3; i++)
            await _sut.Invoking(s => s.LoginAsync(new LoginRequest("user@email.com", "mala"), CancellationToken.None))
                .Should().ThrowAsync<AuthenticationFailedException>();

        var accion = () => _sut.LoginAsync(new LoginRequest("user@email.com", "123456"), CancellationToken.None);

        (await accion.Should().ThrowAsync<AuthenticationFailedException>()).Which.Code.Should().Be("USUARIO_BLOQUEADO");
        _usuario.Bloqueado.Should().BeTrue();
    }
}
