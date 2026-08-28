using ArandaGateway.Api.Integrations.Aranda;
using Microsoft.AspNetCore.Diagnostics;

namespace ArandaGateway.Api.Observability;

public sealed class GatewayExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GatewayExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, errorCode) = exception switch
        {
            ArandaApiException arandaException =>
                (
                    StatusCodes.Status502BadGateway,
                    "No se pudo completar la operación con Aranda.",
                    $"ARANDA_{(int)arandaException.StatusCode}"),
            ArandaContractException =>
                (
                    StatusCodes.Status502BadGateway,
                    "Aranda devolvió una respuesta no válida.",
                    "ARANDA_INVALID_RESPONSE"),
            HttpRequestException =>
                (
                    StatusCodes.Status502BadGateway,
                    "No se pudo establecer comunicación con Aranda.",
                    "ARANDA_CONNECTION_ERROR"),
            TaskCanceledException =>
                (
                    StatusCodes.Status504GatewayTimeout,
                    "Aranda no respondió dentro del tiempo permitido.",
                    "ARANDA_TIMEOUT"),
            _ =>
                (
                    StatusCodes.Status500InternalServerError,
                    "Ocurrió un error inesperado.",
                    "UNEXPECTED_ERROR")
        };

        logger.LogError(
            "Gateway request failed. TraceId: {TraceId}, " +
            "ErrorCode: {ErrorCode}, ExceptionType: {ExceptionType}",
            httpContext.TraceIdentifier,
            errorCode,
            exception.GetType().Name);

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(
            new()
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = title,
                    Extensions =
                    {
                        ["errorCode"] = errorCode,
                        ["traceId"] = httpContext.TraceIdentifier
                    }
                }
            });
    }
}
