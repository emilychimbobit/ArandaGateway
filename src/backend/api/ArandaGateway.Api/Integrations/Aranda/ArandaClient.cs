using System.Net.Http.Json;
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

    public Task<ArandaTicket> CreateTicketAsync(
        ArandaCreateTicketRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<ArandaCreateTicketRequest, ArandaTicket>(
            "api/v9/item/",
            request,
            cancellationToken);

    public Task<ArandaTicket> UpdateTicketAsync(
        long ticketId,
        ArandaUpdateTicketRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<ArandaUpdateTicketRequest, ArandaTicket>(
            $"api/v9/item/{ticketId}",
            request,
            cancellationToken);

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
