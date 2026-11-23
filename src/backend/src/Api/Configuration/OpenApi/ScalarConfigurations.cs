using System.Text.Json.Nodes;
using Api.Modules.Authorization.Policies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Api.Configuration.OpenApi;

public static class ScalarConfigurations
{
    public static IServiceCollection AddScalar(this IServiceCollection service)
    {
        service.AddTransient<IApiDescriptionProvider, WolverineResponseTypes>();
        service.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                var httpContextAccessor = context.ApplicationServices.GetService<IHttpContextAccessor>();
                var httpContext = httpContextAccessor?.HttpContext;

                if (httpContext == null) return Task.CompletedTask;
                var host = httpContext.Request.Host.ToString();
                var scheme = httpContext.Request.Scheme;
                var baseUrl = $"{scheme}://{host}";
                document.Servers?.Clear();
                document.Servers?.Add(new OpenApiServer
                {
                    Url = baseUrl,
                    Description = "Test"
                });

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??=
                    new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                const string authScheme = JwtBearerDefaults.AuthenticationScheme;
                if (!document.Components.SecuritySchemes.ContainsKey(authScheme))
                    document.Components.SecuritySchemes[authScheme] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = authScheme,
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Name = "Authorization",
                        Description = "Enter your JWT as: Bearer {token}"
                    };

                if (document.Components.SecuritySchemes[authScheme] is OpenApiSecurityScheme mutableScheme)
                {
                    mutableScheme.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
                    mutableScheme.Extensions["x-default"] = new JsonNodeExtension(JsonValue.Create(true)!);
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                var endpointMetadata = context.Description?.ActionDescriptor?.EndpointMetadata;
                var metadata = endpointMetadata?.Cast<object>() ?? [];

                var enumerable = metadata as object[] ?? metadata.ToArray();
                var authorizeData = enumerable.OfType<IAuthorizeData>().ToList();

                var hasJwtPolicy = authorizeData.Any(a =>
                    string.Equals(a.Policy, AuthorizationPolicies.JwtPolicy, StringComparison.Ordinal));

                const string bearerSchemeName = JwtBearerDefaults.AuthenticationScheme;
                if (!hasJwtPolicy)
                {
                    operation.Security = new List<OpenApiSecurityRequirement>();
                    return Task.CompletedTask;
                }

                var document = context.Document;
                if (document is null)
                {
                    operation.Security = new List<OpenApiSecurityRequirement>();
                    return Task.CompletedTask;
                }

                var schemeReference = new OpenApiSecuritySchemeReference(bearerSchemeName, document,
                    null);
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new()
                    {
                        [schemeReference] = new List<string>()
                    }
                };

                return Task.CompletedTask;
            });
        });

        return service;
    }

    public static WebApplication UseScalar(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }
}
