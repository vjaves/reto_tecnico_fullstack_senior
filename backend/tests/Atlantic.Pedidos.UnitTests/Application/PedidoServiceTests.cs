using Atlantic.Pedidos.Application.Abstractions;
using Atlantic.Pedidos.Application.Common;
using Atlantic.Pedidos.Application.Pedidos;
using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Pedidos;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Atlantic.Pedidos.UnitTests.Application;

public class PedidoServiceTests
{
    private const int EmpresaId = 1;

    private readonly IPedidoRepository _repositorio = Substitute.For<IPedidoRepository>();
    private readonly IPedidoQueries _consultas = Substitute.For<IPedidoQueries>();
    private readonly ICatalogoQueries _catalogos = Substitute.For<ICatalogoQueries>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _usuario = Substitute.For<ICurrentUser>();
    private readonly PedidoService _sut;

    public PedidoServiceTests()
    {
        _usuario.UsuarioId.Returns(10);
        _usuario.EmpresaId.Returns(EmpresaId);
        _catalogos.ClienteValidoAsync(EmpresaId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _catalogos.SucursalValidaAsync(EmpresaId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _catalogos.MonedaValidaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _catalogos.ProductosValidosAsync(EmpresaId, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IReadOnlyCollection<int>>().ToHashSet());
        _consultas.ObtenerAsync(Arg.Any<int>(), EmpresaId, Arg.Any<CancellationToken>())
            .Returns(new PedidoDto(0, "PED-100", 1, "Cliente", 1, "Sede", 1, "PEN", new DateOnly(2026, 9, 24),
                0, 10, "Registrado", DateTime.Now, null, "", []));

        _sut = new PedidoService(_repositorio, _consultas, _catalogos, _uow, _usuario, TimeProvider.System,
            new CrearPedidoRequestValidator(), new ActualizarPedidoRequestValidator(), new PedidoFiltroValidator(),
            NullLogger<PedidoService>.Instance);
    }

    private static CrearPedidoRequest RequestValido(string numero = "PED-100") => new()
    {
        NumeroPedido = numero,
        ClienteId = 1,
        SucursalId = 1,
        MonedaId = 1,
        Fecha = new DateOnly(2026, 9, 24),
        Detalles = [new PedidoLineaRequest(null, 1, 1m, 10m)]
    };

    [Fact]
    public async Task Crear_persiste_el_pedido_cuando_todo_es_valido()
    {
        await _sut.CrearAsync(RequestValido(), CancellationToken.None);

        _repositorio.Received(1).Agregar(Arg.Is<Pedido>(p => p.NumeroPedido == "PED-100" && p.TotalPedido == 10m
                                                           && p.UsuarioCreacionId == 10));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_con_numero_existente_responde_conflicto_y_no_guarda()
    {
        _repositorio.ExisteNumeroAsync("PED-100", null, Arg.Any<CancellationToken>()).Returns(true);

        var accion = () => _sut.CrearAsync(RequestValido("ped-100"), CancellationToken.None);

        (await accion.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("PEDIDO_NUMERO_DUPLICADO");
        _repositorio.DidNotReceive().Agregar(Arg.Any<Pedido>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_con_datos_invalidos_lanza_validacion_antes_de_ir_a_la_bd()
    {
        var accion = () => _sut.CrearAsync(RequestValido() with { NumeroPedido = "", Detalles = [] }, CancellationToken.None);

        await accion.Should().ThrowAsync<ValidationException>();
        await _repositorio.DidNotReceiveWithAnyArgs().ExisteNumeroAsync(default!, default, default);
    }

    [Fact]
    public async Task Crear_con_cliente_de_otra_empresa_se_rechaza()
    {
        _catalogos.ClienteValidoAsync(EmpresaId, 1, Arg.Any<CancellationToken>()).Returns(false);

        var accion = () => _sut.CrearAsync(RequestValido(), CancellationToken.None);

        (await accion.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("CLIENTE_INVALIDO");
    }

    [Fact]
    public async Task Actualizar_con_rowversion_desactualizado_responde_conflicto()
    {
        var pedido = Pedido.Crear("PED-100", 1, 1, 1, DateTime.Today, [new LineaPedido(null, 1, 1m, 10m, 0m)], 1, DateTime.Now);
        _repositorio.ObtenerAsync(5, EmpresaId, Arg.Any<CancellationToken>()).Returns(pedido);
        var request = new ActualizarPedidoRequest
        {
            NumeroPedido = "PED-100", ClienteId = 1, SucursalId = 1, MonedaId = 1, Fecha = new DateOnly(2026, 9, 24),
            Estado = "Confirmado", RowVersion = "AAAAAAAAB9M=",
            Detalles = [new PedidoLineaRequest(null, 1, 1m, 10m)]
        };

        var accion = () => _sut.ActualizarAsync(5, request, CancellationToken.None);

        (await accion.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("PEDIDO_MODIFICADO");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eliminar_un_pedido_inexistente_responde_no_encontrado()
    {
        var accion = () => _sut.EliminarAsync(99, CancellationToken.None);

        await accion.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Eliminar_anula_logicamente_y_guarda()
    {
        var pedido = Pedido.Crear("PED-100", 1, 1, 1, DateTime.Today, [new LineaPedido(null, 1, 1m, 10m, 0m)], 1, DateTime.Now);
        _repositorio.ObtenerAsync(5, EmpresaId, Arg.Any<CancellationToken>()).Returns(pedido);

        await _sut.EliminarAsync(5, CancellationToken.None);

        pedido.Estado.Should().Be(EstadoPedido.Anulado);
        pedido.UsuarioAnuladoId.Should().Be(10);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
