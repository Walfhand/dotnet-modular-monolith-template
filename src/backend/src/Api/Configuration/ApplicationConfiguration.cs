using System.Reflection;
using Api.Configuration.Integrations;
using Api.Configuration.OpenApi;
using Api.Infrastructure.Persistence.Configs;
using Engine.Core.Events;
using Engine.Logging;
using Engine.ProblemDetails;
using Engine.Wolverine;
using Wolverine.Http;

namespace Api.Configuration;

internal static class ApplicationConfiguration
{
    public static WebApplicationBuilder AddEngineModules(this WebApplicationBuilder builder, Assembly assembly)
    {
        builder.Host.UseCustomWolverine(builder.Configuration, assembly);
        builder.Host.AddCustomLogging();

        builder.Services.AddCustomProblemDetails();
        builder.Services.AddWolverineHttp();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddData();
        builder.Services.AddScalar();


        builder.Services.AddSingleton<IEventMapper, EventMapper>();
        return builder;
    }

    public static WebApplication UseApplication(this WebApplication app)
    {
        app.MapWolverineEndpoints();
        app.UseScalar();
        app.UseMigration();
        return app;
    }
}
