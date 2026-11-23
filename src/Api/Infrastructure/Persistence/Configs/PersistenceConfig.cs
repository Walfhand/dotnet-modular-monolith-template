using Engine.EFCore;

namespace Api.Infrastructure.Persistence.Configs;

public static class PersistenceConfig
{
    public static IServiceCollection AddData(this IServiceCollection services)
    {
        return services.AddCustomDbContext<AppDbContext>();
    }

    public static WebApplication UseMigration(this WebApplication app)
    {
        app.UseMigration<AppDbContext>();
        return app;
    }
}
