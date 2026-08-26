namespace ArandaGateway.Api.Application.Tickets;

public interface ITicketService
{
    Task<TicketDetailResult> GetTicketDetailAsync(
        long caseNumber,
        CancellationToken cancellationToken);
}
