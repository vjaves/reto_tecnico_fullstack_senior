using Atlantic.Pedidos.Infrastructure.Resilience;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;

namespace Atlantic.Pedidos.UnitTests.Infrastructure;

public class SqlResilienceTests
{
    private static readonly SqlResilienceOptions Opciones = new()
    {
        MaxReintentos = 2,
        RetrasoBaseMs = 10,
        TimeoutSegundos = 5,
        ProporcionFallos = 0.5,
        MinimoEjecuciones = 2,
        VentanaSegundos = 30,
        AperturaSegundos = 30
    };

    private static (ResiliencePipeline Pipeline, CircuitBreakerStateProvider Estado) Construir(SqlResilienceOptions? opciones = null)
    {
        var estado = new CircuitBreakerStateProvider();
        var builder = new ResiliencePipelineBuilder();
        SqlResilience.Configurar(builder, opciones ?? Opciones, estado);
        return (builder.Build(), estado);
    }

    [Fact]
    public async Task Un_error_transitorio_se_reintenta_hasta_tener_exito()
    {
        // Umbral de apertura alto: aquí sólo interesa el reintento, no que el circuito corte antes.
        var (pipeline, estado) = Construir(new SqlResilienceOptions { MaxReintentos = 2, RetrasoBaseMs = 10, MinimoEjecuciones = 100 });
        var intentos = 0;

        var resultado = await pipeline.ExecuteAsync(_ =>
        {
            intentos++;
            if (intentos < 3) throw new TimeoutException("timeout simulado");
            return ValueTask.FromResult("ok");
        });

        resultado.Should().Be("ok");
        intentos.Should().Be(3, "1 intento + 2 reintentos");
        estado.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task Un_error_de_negocio_no_se_reintenta_ni_abre_el_circuito()
    {
        var (pipeline, estado) = Construir();
        var intentos = 0;

        var accion = () => pipeline.ExecuteAsync<string>(_ =>
        {
            intentos++;
            throw new InvalidOperationException("regla de negocio");
        }).AsTask();

        for (var i = 0; i < 5; i++)
            await accion.Should().ThrowAsync<InvalidOperationException>();

        intentos.Should().Be(5, "sin reintentos: un error no transitorio falla a la primera");
        estado.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task Fallos_transitorios_sostenidos_abren_el_circuito_y_luego_se_falla_rapido()
    {
        var (pipeline, estado) = Construir();
        var llamadasALaBd = 0;

        Func<Task> fallaSiempre = () => pipeline.ExecuteAsync<string>(_ =>
        {
            llamadasALaBd++;
            throw new TimeoutException("SQL Server no responde");
        }).AsTask();

        // El circuito se abre en cuanto hay 2 ejecuciones con ≥ 50 % de fallos; el reintento que
        // choca con el circuito abierto recibe BrokenCircuitException.
        await fallaSiempre.Should().ThrowAsync<Exception>();
        estado.CircuitState.Should().Be(CircuitState.Open);

        var llamadasAntes = llamadasALaBd;
        var rechazo = await fallaSiempre.Should().ThrowAsync<BrokenCircuitException>();
        llamadasALaBd.Should().Be(llamadasAntes, "con el circuito abierto no se toca la BD");
        rechazo.Which.RetryAfter.Should().NotBeNull();
    }

    [Theory]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(ArgumentException), false)]
    public void Clasifica_que_errores_son_transitorios(Type tipo, bool esperado) =>
        SqlResilience.EsTransitorio((Exception)Activator.CreateInstance(tipo)!).Should().Be(esperado);

    [Fact]
    public void Un_circuito_abierto_cuenta_como_fallo_de_infraestructura() =>
        SqlResilience.EsFalloDeInfraestructura(new BrokenCircuitException()).Should().BeTrue();
}
