using ArandaGateway.Api.Contracts.Tickets;

namespace ArandaGateway.Api.Application.Tickets;

public interface ITicketService
{
    Task<TicketOperationResult<CreateTicketResponse>> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken);

    Task<TicketOperationResult<IReadOnlyList<TicketSummaryResponse>>>
        ListOpenTicketsAsync(CancellationToken cancellationToken);

    Task<TicketDetailResult> GetTicketDetailAsync(
        string caseNumber,
        CancellationToken cancellationToken);

    Task<TicketOperationResult<CancelTicketResponse>> CancelTicketAsync(
        string caseNumber,
        CancelTicketRequest request,
        CancellationToken cancellationToken);

    Task<TicketOperationResult<UploadAttachmentResponse>>
        UploadAttachmentAsync(
            string caseNumber,
            TicketAttachment attachment,
            CancellationToken cancellationToken);
}
