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
            cancellationToken);

    public Task<ArandaCreatedTicket> CreateTicketAsync(
        ArandaCreateTicketRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<ArandaCreateTicketRequest, ArandaCreatedTicket>(
            "api/v9/item/",
            request,
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

        using var response = await httpClient.PostAsync(
            "api/v9/file/",
            content,
            cancellationToken);

        return await ReadResponseAsync<
            IReadOnlyList<ArandaFileUploadResult>>(
                response,
                cancellationToken);
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            requestUri,
            request,
            cancellationToken);

        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            requestUri,
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
            throw new ArandaApiException(response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(
            cancellationToken)
            ?? throw new ArandaApiException(
                response.StatusCode,
                "Aranda returned an empty response.");
    }
}
