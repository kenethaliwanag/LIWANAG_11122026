using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CsvProcessing.Api.Infrastructure;

public sealed class ApiKeyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var allowsAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (allowsAnonymous)
        {
            return;
        }

        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter =>
                string.Equals(parameter.Name, ApiKeyDefaults.HeaderName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = ApiKeyDefaults.HeaderName,
            In = ParameterLocation.Header,
            Description = "API key required for every request except /health.",
            Required = true
        });
    }
}
