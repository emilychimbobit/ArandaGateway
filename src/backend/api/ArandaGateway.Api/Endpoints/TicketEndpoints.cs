using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;

namespace ArandaGateway.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/tickets")
            .WithTags("Tickets");

        group
            .MapGet("/{caseNumber:long}", GetTicketDetailAsync)
            .WithName("GetTicketDetail")
            .Produces<TicketDetailResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetTicketDetailAsync(
        long caseNumber,
        ITicketService ticketService,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.GetTicketDetailAsync(
            caseNumber,
            cancellationToken);

        return result.Status switch
        {
            TicketDetailResultStatus.Success =>
                Results.Ok(result.Ticket),
            TicketDetailResultStatus.MissingCollaborator =>
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "No se pudo identificar al colaborador.",
                    detail:
                        $"Envíe el encabezado {HeaderCurrentCollaborator.HeaderName}."),
            TicketDetailResultStatus.NotFoundOrNotOwned =>
                Results.NotFound(),
            _ => throw new InvalidOperationException(
                $"Unsupported ticket result: {result.Status}.")
        };
    }
}
