namespace ScrapGo.Core.Shared.Kernel.Quickbase;

/// <summary>A failed Quickbase call. <see cref="StatusCode"/> is null when no response arrived (timeout, network failure).</summary>
public sealed class QuickbaseApiException(string message, int? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
