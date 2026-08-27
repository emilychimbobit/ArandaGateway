using System.Net;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;

namespace ArandaGateway.Api.Application.Tickets;

public sealed class TicketService(
    ICurrentCollaborator currentCollaborator,
    IArandaClient arandaClient) : ITicketService
{
    public async Task<TicketDetailResult> GetTicketDetailAsync(
        long caseNumber,
        CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return new(TicketDetailResultStatus.MissingCollaborator);
        }

        try
        {
            var ticket = await arandaClient.GetTicketAsync(
                caseNumber,
                cancellationToken);

            if (!string.Equals(
                    ticket.CustomerUserName,
                    username,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new(TicketDetailResultStatus.NotFoundOrNotOwned);
            }

            return new(
                TicketDetailResultStatus.Success,
                new TicketDetailResponse(
                    ticket.IdByProject,
                    ticket.StateName,
                    ticket.GroupName,
                    DateTimeOffset.FromUnixTimeMilliseconds(
                        ticket.ModifiedDate),
                    null));
        }
        catch (ArandaApiException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new(TicketDetailResultStatus.NotFoundOrNotOwned);
        }
    }
}
