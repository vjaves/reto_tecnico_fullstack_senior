using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Atlantic.Pedidos.Api.OpenApi;

public static class SwaggerSetup
{
    public static IServiceCollection AddSwaggerConJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Atlantic Pedidos API",
                Version = "v1",
                Description = "Autenticación JWT y CRUD de pedidos. Use POST /auth/login y pegue el token en Authorize."
            });

            var esquema = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Pegue sólo el token (sin el prefijo Bearer).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            options.AddSecurityDefinition("Bearer", esquema);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = [] });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
                options.IncludeXmlComments(xml);
        });
        return services;
    }
}
