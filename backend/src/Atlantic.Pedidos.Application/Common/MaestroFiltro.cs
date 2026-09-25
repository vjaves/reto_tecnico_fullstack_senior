using FluentValidation;

namespace Atlantic.Pedidos.Application.Common;

/// <summary>Filtro común de los mantenedores (clientes, productos).</summary>
public sealed record MaestroFiltro
{
    public string? Buscar { get; init; }

    /// <summary>activos | inactivos | (vacío = todos). Los eliminados nunca se listan.</summary>
    public string? Estado { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    public bool? SoloActivos => Estado?.ToLowerInvariant() switch
    {
        "activos" => true,
        "inactivos" => false,
        _ => null
    };
}

public sealed class MaestroFiltroValidator : AbstractValidator<MaestroFiltro>
{
    public MaestroFiltroValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Buscar).MaximumLength(100);
        RuleFor(x => x.Estado)
            .Must(e => string.IsNullOrEmpty(e) || e.Equals("activos", StringComparison.OrdinalIgnoreCase)
                                               || e.Equals("inactivos", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Estado no válido. Valores: activos, inactivos.");
    }
}
