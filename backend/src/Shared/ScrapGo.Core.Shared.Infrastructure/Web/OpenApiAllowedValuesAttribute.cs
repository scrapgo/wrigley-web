namespace ScrapGo.Core.Shared.Infrastructure.Web;

/// <summary>The values a parameter accepts, listed in the OpenAPI document (Swagger shows a pick list).</summary>
public interface IOpenApiAllowedValues
{
    IReadOnlyList<string> Values { get; }
}

/// <summary>
/// Lists a parameter's accepted values in the OpenAPI document, so Swagger UI shows a
/// pick list instead of a text box (a multi-select for an array). Documentation only:
/// binding and validation are unchanged, so the endpoint still checks the value itself.
/// </summary>
/// <param name="source">
/// A type implementing <see cref="IOpenApiAllowedValues"/> with a parameterless
/// constructor; it reads the values from their single source (e.g. a dropdown catalog).
/// </param>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class OpenApiAllowedValuesAttribute(Type source) : Attribute
{
    public Type Source { get; } = typeof(IOpenApiAllowedValues).IsAssignableFrom(source)
        ? source
        : throw new ArgumentException($"{source} does not implement {nameof(IOpenApiAllowedValues)}.", nameof(source));

    public IReadOnlyList<string> Values() => ((IOpenApiAllowedValues)Activator.CreateInstance(Source)!).Values;
}
