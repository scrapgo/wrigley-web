using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ScrapGo.Core.Shared.Infrastructure.Web;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ScrapGo.Core.Api.Composition;

/// <summary>
/// Turns <see cref="OpenApiAllowedValuesAttribute"/> into the parameter schema's <c>enum</c>
/// (the items' for an array), so Swagger UI shows a pick list.
/// </summary>
internal sealed class AllowedValuesParameterFilter : IParameterFilter
{
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        var attribute = context.ParameterInfo?.GetCustomAttribute<OpenApiAllowedValuesAttribute>()
            ?? context.PropertyInfo?.GetCustomAttribute<OpenApiAllowedValuesAttribute>();
        if (attribute is null || parameter.Schema is not OpenApiSchema schema)
        {
            return;
        }

        var target = schema.Items as OpenApiSchema ?? schema;
        target.Enum = [.. attribute.Values().Select(value => (JsonNode)JsonValue.Create(value))];
    }
}
