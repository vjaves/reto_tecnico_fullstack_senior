using Atlantic.Pedidos.Application.Auth;
using Atlantic.Pedidos.Application.Catalogos;
using Atlantic.Pedidos.Application.Clientes;
using Atlantic.Pedidos.Application.Productos;
using Atlantic.Pedidos.Application.Usuarios;
using Atlantic.Pedidos.Application.Pedidos;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlantic.Pedidos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<ICatalogoService, CatalogoService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IUsuarioAdminService, UsuarioAdminService>();
        services.AddScoped<IValidadorSesion, ValidadorSesion>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
