using System.Net;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaApiException : Exception
{
    public ArandaApiException(
        HttpStatusCode statusCode,
        string message = "Aranda rejected the request.")
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
