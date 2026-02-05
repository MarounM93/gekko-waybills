using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gekko.Waybills.Api.Swagger;

public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= [];

        if (operation.Parameters.Any(p =>
                string.Equals(p.Name, "X-Tenant-ID", StringComparison.OrdinalIgnoreCase) &&
                p.In == ParameterLocation.Header))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-ID",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Tenant identifier (required by middleware).",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
