using Atlantic.Pedidos.Domain.Pedidos;
using FluentValidation;

namespace Atlantic.Pedidos.Application.Pedidos;

// Validación de forma (400). Las reglas de negocio (total > 0, transición de estados,
// número único) viven en el dominio y en el servicio, y responden 422 / 409.

public abstract class PedidoRequestBaseValidator<T> : AbstractValidator<T> where T : PedidoRequestBase
{
    protected PedidoRequestBaseValidator()
    {
        RuleFor(x => x.NumeroPedido)
            .NotEmpty().WithMessage("El número de pedido es obligatorio.")
            .MaximumLength(20).WithMessage("El número de pedido admite como máximo 20 caracteres.")
            .Matches("^[A-Za-z0-9-]+$").WithMessage("El número de pedido sólo admite letras, números y guiones.");

        RuleFor(x => x.ClienteId).GreaterThan(0).WithMessage("Seleccione un cliente.");
        RuleFor(x => x.SucursalId).GreaterThan(0).WithMessage("Seleccione una sucursal.");
        RuleFor(x => x.MonedaId).GreaterThan(0).WithMessage("Seleccione una moneda.");

        RuleFor(x => x.Fecha)
            .NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.")
            .GreaterThanOrEqualTo(new DateOnly(2000, 1, 1)).WithMessage("La fecha no es válida.");

        RuleFor(x => x.Detalles)
            .NotEmpty().WithMessage("El pedido debe tener al menos una línea.")
            .Must(d => d.Count <= 100).WithMessage("El pedido admite como máximo 100 líneas.");

        RuleFor(x => x.Detalles)
            .Must(d => d.Where(l => l.Id.HasValue).Select(l => l.Id).Distinct().Count() == d.Count(l => l.Id.HasValue))
            .WithMessage("Hay líneas repetidas.");

        RuleForEach(x => x.Detalles).ChildRules(linea =>
        {
            linea.RuleFor(l => l.ProductoId).GreaterThan(0).WithMessage("Seleccione un producto.");
            linea.RuleFor(l => l.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0.")
                .LessThan(1_000_000m).WithMessage("La cantidad es demasiado grande.")
                .PrecisionScale(19, 4, true).WithMessage("La cantidad admite hasta 4 decimales.");
            linea.RuleFor(l => l.PrecioVenta).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.")
                .LessThan(100_000_000m).WithMessage("El precio es demasiado grande.")
                .PrecisionScale(18, 6, true).WithMessage("El precio admite hasta 6 decimales.");
            linea.RuleFor(l => l.Descuento).GreaterThanOrEqualTo(0).WithMessage("El descuento no puede ser negativo.")
                .PrecisionScale(12, 2, true).WithMessage("El descuento admite hasta 2 decimales.");
        });
    }
}

public sealed class CrearPedidoRequestValidator : PedidoRequestBaseValidator<CrearPedidoRequest>;

public sealed class ActualizarPedidoRequestValidator : PedidoRequestBaseValidator<ActualizarPedidoRequest>
{
    public ActualizarPedidoRequestValidator()
    {
        RuleFor(x => x.Estado)
            .NotEmpty().WithMessage("El estado es obligatorio.")
            .Must(e => Enum.TryParse<EstadoPedido>(e, true, out var v) && Enum.IsDefined(v) && !int.TryParse(e, out _))
            .WithMessage($"Estado no válido. Valores: {string.Join(", ", Enum.GetNames<EstadoPedido>())}.");

        RuleFor(x => x.RowVersion)
            .Must(v => v is null || Convert.TryFromBase64String(v, new byte[v.Length], out _))
            .WithMessage("rowVersion no es válido.");
    }
}

public sealed class PedidoFiltroValidator : AbstractValidator<PedidoFiltro>
{
    private static readonly string[] Ordenes = ["numero", "cliente", "fecha", "total", "estado"];

    public PedidoFiltroValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Buscar).MaximumLength(100);
        RuleFor(x => x.Estado)
            .Must(e => e is null || Enum.TryParse<EstadoPedido>(e, true, out _))
            .WithMessage("Estado no válido.");
        RuleFor(x => x.OrdenarPor)
            .Must(o => o is null || Ordenes.Contains(o, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Orden no válido. Valores: {string.Join(", ", Ordenes)}.");
        RuleFor(x => x)
            .Must(x => x.Desde is null || x.Hasta is null || x.Desde <= x.Hasta)
            .WithMessage("La fecha 'desde' no puede ser posterior a 'hasta'.");
    }
}
