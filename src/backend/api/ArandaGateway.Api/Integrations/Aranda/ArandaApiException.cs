using System.Net;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaApiException : Exception
{
    public ArandaApiException(
        HttpStatusCode statusCode,
        string? message = null,
        string? requestUri = null,
        string? responseBody = null)
        : base(message ?? BuildMessage(statusCode, requestUri))
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>Operación que falló, para no diagnosticar a ciegas.</summary>
    public string? RequestUri { get; }

    /// <summary>
    /// Respuesta de Aranda, recortada. Distingue casos que comparten código:
    /// un 401 de APIM por falta de clave de suscripción no se parece al 401 de
    /// Aranda por sesión caducada.
    ///
    /// Queda fuera de <see cref="Exception.Message"/> a propósito: es un dato
    /// del proveedor y solo debe llegar al log del servidor, nunca a la
    /// respuesta que ve el consumidor.
    /// </summary>
    public string? ResponseBody { get; }

    private static string BuildMessage(
        HttpStatusCode statusCode,
        string? requestUri)
    {
        var message =
            $"Aranda rejected the request with status code {(int)statusCode}.";

        if (!string.IsNullOrWhiteSpace(requestUri))
        {
            message += $" Request: {requestUri}.";
        }

        return message;
    }
}
