using Atlantic.Pedidos.Domain.Catalogos;
using Atlantic.Pedidos.Domain.Common;
using FluentAssertions;

namespace Atlantic.Pedidos.UnitTests.Domain;

public class MaestrosTests
{
    private static readonly DateTime Ahora = new(2026, 9, 24, 10, 0, 0);

    // TipoDocumento es un catálogo de sólo lectura sin fábrica pública: en BD viene sembrado.
    private static TipoDocumento Tipo(int id, string abreviatura)
    {
        var tipo = (TipoDocumento)Activator.CreateInstance(typeof(TipoDocumento), nonPublic: true)!;
        typeof(TipoDocumento).GetProperty(nameof(TipoDocumento.Id))!.SetValue(tipo, id);
        typeof(TipoDocumento).GetProperty(nameof(TipoDocumento.Abreviatura))!.SetValue(tipo, abreviatura);
        return tipo;
    }

    private static readonly TipoDocumento Dni = Tipo(1, "DNI");
    private static readonly TipoDocumento Ruc = Tipo(2, "RUC");

    private static DatosCliente Persona(string doc = "45781236") =>
        new(doc, "Juan", null, "Perez", null, "IGNORAR S.A.C.", null, " Av. Lima 123 ", null, true);

    [Theory]
    [InlineData("DNI", "1234567", "El DNI debe tener 8 dígitos.")]
    [InlineData("DNI", "1234567A", "El DNI debe tener 8 dígitos.")]
    [InlineData("RUC", "2051234567", "El RUC debe tener 11 dígitos.")]
    [InlineData("RUC", "30512345678", "El RUC debe empezar con 10, 15, 17 o 20.")]
    [InlineData("DNI", "45781236", null)]
    [InlineData("RUC", "20512345678", null)]
    public void El_documento_se_valida_segun_su_tipo(string tipo, string numero, string? error) =>
        Cliente.ValidarDocumento(tipo, numero).Should().Be(error);

    [Fact]
    public void Persona_natural_descarta_la_razon_social_y_limpia_espacios()
    {
        var cliente = Cliente.Crear(1, Dni, Persona(), usuarioId: 5, Ahora);

        cliente.RazonSocial.Should().BeNull("el nombre visible es RazonSocial ?? nombres");
        cliente.PrimerNombre.Should().Be("Juan");
        cliente.Direccion.Should().Be("Av. Lima 123");
        cliente.UsuarioCreacionId.Should().Be(5);
    }

    [Fact]
    public void Un_RUC_exige_razon_social_y_descarta_los_nombres()
    {
        var sinRazon = () => Cliente.Crear(1, Ruc, Persona("20512345678") with { RazonSocial = " " }, 1, Ahora);
        sinRazon.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("CLIENTE_RAZON_SOCIAL");

        var empresa = Cliente.Crear(1, Ruc, Persona("20512345678"), 1, Ahora);
        empresa.RazonSocial.Should().Be("IGNORAR S.A.C.");
        empresa.PrimerNombre.Should().BeNull();
        empresa.ApellidoPaterno.Should().BeNull();
    }

    [Fact]
    public void Eliminar_un_cliente_es_logico_y_lo_desactiva()
    {
        var cliente = Cliente.Crear(1, Dni, Persona(), 1, Ahora);

        cliente.Eliminar(usuarioId: 9, Ahora);

        cliente.Eliminado.Should().BeTrue();
        cliente.Estado.Should().BeFalse();
        cliente.UsuarioAnulacionId.Should().Be(9);
        cliente.Invoking(c => c.Actualizar(Dni, Persona(), 1, Ahora))
            .Should().Throw<BusinessRuleException>().Which.Code.Should().Be("CLIENTE_ELIMINADO");
    }

    [Fact]
    public void Producto_normaliza_el_codigo_y_su_baja_es_logica()
    {
        var producto = Producto.Crear(1, new DatosProducto(" p-011 ", " Cable HDMI ", "", 1, true), 1, Ahora);

        producto.CodigoProducto.Should().Be("P-011");
        producto.Nombre.Should().Be("Cable HDMI");
        producto.Descripcion.Should().BeNull();

        producto.Eliminar(2, Ahora);
        producto.Eliminado.Should().BeTrue();
        producto.Estado.Should().BeFalse();
        producto.Invoking(p => p.Eliminar(2, Ahora)).Should().Throw<BusinessRuleException>();
    }
}
