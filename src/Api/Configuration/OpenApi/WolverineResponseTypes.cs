using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Wolverine.Http;

namespace Api.Configuration.OpenApi;

internal sealed class WolverineResponseTypes : IApiDescriptionProvider
{
    public int Order => 0;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        foreach (var description in context.Results)
        {
            if (description.ActionDescriptor is not WolverineActionDescriptor endpoint)
                continue;

            // Wolverine's ApiExplorer keeps only the first body type per HTTP status.
            foreach (var metadata in endpoint.EndpointMetadata.OfType<IProducesResponseTypeMetadata>())
            {
                if (metadata.Type is not { } type || type == typeof(void))
                    continue;

                var responses = description.SupportedResponseTypes;
                if (responses.Any(response => response.StatusCode == metadata.StatusCode && response.Type == type))
                    continue;

                responses.Add(new ApiResponseType
                {
                    StatusCode = metadata.StatusCode,
                    Type = type,
                    ApiResponseFormats = metadata.ContentTypes.Select(contentType =>
                        new ApiResponseFormat { MediaType = contentType }).ToList()
                });
            }
        }
    }
}
