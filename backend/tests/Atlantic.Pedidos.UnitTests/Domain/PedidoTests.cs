using Atlantic.Pedidos.Domain.Common;
using Atlantic.Pedidos.Domain.Pedidos;
using FluentAssertions;

namespace Atlantic.Pedidos.UnitTests.Domain;

public class PedidoTests
{
    private static readonly DateTime Ahora = new(2026, 9, 24, 10, 0, 0);

    private static Pedido NuevoPedido(params LineaPedido[] lineas) =>
        Pedido.Crear("ped-001 ", sucursalId: 1, clienteId: 1, monedaId: 1, Ahora, lineas, usuarioId: 1, Ahora);

    [Fact]
    public void Crear_calcula_totales_y_normaliza_el_numero()
    {
        var pedido = NuevoPedido(
            new LineaPedido(null, 1, 2m, 100m, 10m),
            new LineaPedido(null, 2, 3m, 33.333m, 0m));

        pedido.NumeroPedido.Should().Be("PED-001");
        pedido.Estado.Should().Be(EstadoPedido.Registrado);
        pedido.TotalDescuentos.Should().Be(10m);
        // 2×100 − 10 = 190 ; 3×33.333 = 99.999 → 100.00
        pedido.TotalPedido.Should().Be(290m);
        pedido.Detalles.Select(d => d.Item).Should().Equal(1, 2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    public void Crear_con_total_cero_se_rechaza(decimal precio, decimal descuento)
    {
        var accion = () => NuevoPedido(new LineaPedido(null, 1, 1m, precio, descuento));

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PEDIDO_TOTAL_INVALIDO");
    }

    [Fact]
    public void Crear_sin_lineas_se_rechaza()
    {
        var accion = () => NuevoPedido();

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PEDIDO_SIN_LINEAS");
    }

    [Fact]
    public void Descuento_mayor_al_subtotal_se_rechaza()
    {
        var accion = () => NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 11m));

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("DETALLE_DESCUENTO_EXCEDE");
    }

    [Fact]
    public void Actualizar_no_permite_retroceder_de_estado()
    {
        var pedido = NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 0m));
        var lineas = new[] { new LineaPedido(null, 1, 1m, 10m, 0m) };
        pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Despachado, lineas, 1, Ahora);

        var accion = () => pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Confirmado, lineas, 1, Ahora);

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("ESTADO_RETROCESO");
    }

    [Fact]
    public void Actualizar_a_anulado_obliga_a_usar_eliminar()
    {
        var pedido = NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 0m));

        var accion = () => pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Anulado,
            [new LineaPedido(null, 1, 1m, 10m, 0m)], 1, Ahora);

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("ESTADO_ANULADO_NO_PERMITIDO");
    }

    [Fact]
    public void Actualizar_da_de_baja_logica_las_lineas_que_no_vienen()
    {
        var pedido = NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 0m));

        pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Registrado,
            [new LineaPedido(null, 2, 5m, 4m, 0m)], usuarioId: 2, Ahora);

        pedido.Detalles.Should().ContainSingle().Which.ProductoId.Should().Be(2);
        pedido.TotalPedido.Should().Be(20m);
        pedido.UsuarioModificacionId.Should().Be(2);
    }

    [Fact]
    public void Actualizar_con_linea_ajena_se_rechaza()
    {
        var pedido = NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 0m));

        var accion = () => pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Registrado,
            [new LineaPedido(12345, 1, 1m, 10m, 0m)], 1, Ahora);

        accion.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("DETALLE_NO_PERTENECE");
    }

    [Fact]
    public void Anular_es_logico_y_no_se_repite()
    {
        var pedido = NuevoPedido(new LineaPedido(null, 1, 1m, 10m, 0m));

        pedido.Anular(usuarioId: 7, Ahora);

        pedido.Estado.Should().Be(EstadoPedido.Anulado);
        pedido.EstaAnulado.Should().BeTrue();
        pedido.UsuarioAnuladoId.Should().Be(7);
        pedido.Invoking(p => p.Anular(7, Ahora))
            .Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PEDIDO_YA_ANULADO");
    }

    [Fact]
    public void Un_pedido_entregado_no_se_modifica_ni_se_anula()
    {
        var lineas = new[] { new LineaPedido(null, 1, 1m, 10m, 0m) };
        var pedido = NuevoPedido(lineas);
        pedido.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Entregado, lineas, 1, Ahora);

        pedido.Invoking(p => p.Anular(1, Ahora)).Should().Throw<BusinessRuleException>();
        pedido.Invoking(p => p.Actualizar("PED-001", 1, 1, 1, Ahora, EstadoPedido.Entregado, lineas, 1, Ahora))
            .Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PEDIDO_ENTREGADO");
    }
}
