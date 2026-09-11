using System.Net.Http.Json;
using System.Net.Http.Headers;
using ArandaGateway.Api.Integrations.Aranda.Models;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaClient(HttpClient httpClient) : IArandaClient
{
    public Task<ArandaUser> GetUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken) =>
        GetAsync<ArandaUser>(
            $"api/v9/user/{Uri.EscapeDataString(username)}/detail",
            cancellationToken);

    public Task<ArandaTicket> GetTicketAsync(
        long ticketId,
        CancellationToken cancellationToken) =>
        GetAsync<ArandaTicket>(
            $"api/v9/item/{ticketId}",
            cancellationToken);

    public Task<ArandaPagedResponse<ArandaTicket>> SearchTicketsAsync(
        ArandaSearchTicketsRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<ArandaSearchTicketsRequest, ArandaPagedResponse<ArandaTicket>>(
            "api/v9/item/search?language=0",
            request,
            ArandaRetryPolicy.Reads,
            cancellationToken);

    public Task<ArandaCreatedTicket> CreateTicketAsync(
        ArandaCreateTicketRequest request,
        CancellationToken cancellationToken) =>
        // Crear solo se repite ante un rechazo del borde, donde Aranda no
        // llego a registrar nada: reintentar un 5xx podria duplicar el ticket.
        PostAsync<ArandaCreateTicketRequest, ArandaCreatedTicket>(
            "api/v9/item/",
            request,
            ArandaRetryPolicy.EdgeRejectionsOnly,
            cancellationToken);

    public Task<ArandaUpdateTicketResult> UpdateTicketAsync(
        long ticketId,
        ArandaUpdateTicketRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<ArandaUpdateTicketRequest, ArandaUpdateTicketResult>(
            $"api/v9/item/{ticketId}",
            request,
            cancellationToken);

    public async Task<IReadOnlyList<ArandaFileUploadResult>>
        UploadAttachmentAsync(
            ArandaAttachmentUpload request,
            CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new StringContent(request.TicketId.ToString()),
            "FileItemId");
        content.Add(
            new StringContent(request.ItemType.ToString()),
            "FileItemType");
        content.Add(new StringContent("0"), "UploadType");
        content.Add(new StringContent("0"), "Concept");

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            content.Add(
                new StringContent(request.Description),
                "Description");
        }

        var fileContent = new StreamContent(request.Content);
        fileContent.Headers.ContentType =
            MediaTypeHeaderValue.TryParse(
                request.ContentType,
                out var contentType)
                ? contentType
                : new("application/octet-stream");
        content.Add(fileContent, "Data0", request.FileName);

        using var uploadRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v9/file/")
        {
            Content = content
        };

        // Adjuntar tampoco es una lectura: mismo criterio que crear.
        return await SendAsync<IReadOnlyList<ArandaFileUploadResult>>(
            uploadRequest,
            ArandaRetryPolicy.EdgeRejectionsOnly,
            cancellationToken);
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await SendAsync<TResponse>(
            request,
            ArandaRetryPolicy.Reads,
            cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest body,
        ArandaRetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body)
        };

        return await SendAsync<TResponse>(
            request,
            retryPolicy,
            cancellationToken);
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string requestUri,
        TRequest body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(body)
        };

        // Actualizar no es una lectura: solo se repite si el borde rechazó la
        // llamada antes de que Aranda la ejecutara.
        return await SendAsync<TResponse>(
            request,
            ArandaRetryPolicy.EdgeRejectionsOnly,
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage request,
        ArandaRetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        request.Options.Set(ArandaRetryHandler.PolicyKey, retryPolicy);

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ArandaApiException(
                response.StatusCode,
                requestUri: response.RequestMessage?.RequestUri?.ToString(),
                responseBody: await ReadErrorBodyAsync(
                    response,
                    cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(
            cancellationToken)
            ?? throw new ArandaApiException(
                response.StatusCode,
                "Aranda returned an empty response.");
    }

    /// <summary>
    /// Recorta el cuerpo del error: las páginas de bloqueo (IIS, Cloudflare)
    /// llegan como HTML de varios KB y solo interesa el inicio.
    /// </summary>
    private static async Task<string?> ReadErrorBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        const int maxLength = 300;

        try
        {
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            body = body.Trim();
            return body.Length <= maxLength ? body : body[..maxLength] + "…";
        }
        catch
        {
            // El diagnóstico no debe cambiar el error que se propaga.
            return null;
        }
    }

    public Task<ArandaPagedResponse<ArandaCiItem>> GetCisByUserAndProjectsAsync(
    ArandaCiRequest request,
    CancellationToken cancellationToken) =>
    PostAsync<ArandaCiRequest, ArandaPagedResponse<ArandaCiItem>>(
        "api/v9/ci/cisbyuserandprojects",
        request,
        ArandaRetryPolicy.Reads,
        cancellationToken);
}
