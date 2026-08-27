using ArandaGateway.Api.Integrations.Aranda.Models;

namespace ArandaGateway.Api.Integrations.Aranda;

public interface IArandaClient
{
    Task<ArandaUser> GetUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken);

    Task<ArandaTicket> GetTicketAsync(
        long ticketId,
        CancellationToken cancellationToken);

    Task<ArandaPagedResponse<ArandaTicket>> SearchTicketsAsync(
        ArandaSearchTicketsRequest request,
        CancellationToken cancellationToken);

    Task<ArandaCreatedTicket> CreateTicketAsync(
        ArandaCreateTicketRequest request,
        CancellationToken cancellationToken);

    Task<ArandaUpdateTicketResult> UpdateTicketAsync(
        long ticketId,
        ArandaUpdateTicketRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArandaFileUploadResult>> UploadAttachmentAsync(
        ArandaAttachmentUpload request,
        CancellationToken cancellationToken);
}
