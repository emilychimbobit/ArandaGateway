using System.Net;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaApiException : Exception
{
    public ArandaApiException(
        HttpStatusCode statusCode,
        string? message = null)
        : base(
            message ??
            $"Aranda rejected the request with status code {(int)statusCode}.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
