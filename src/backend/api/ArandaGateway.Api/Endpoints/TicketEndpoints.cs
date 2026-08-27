using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;
using Microsoft.AspNetCore.Mvc;

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
            .WithSummary("Consulta el estado de un ticket propio")
            .WithDescription(
                "Implementa REQ_07. Requiere temporalmente el username " +
                "del propietario en X-Collaborator-Username.")
            .Produces<TicketDetailResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetTicketDetailAsync(
        long caseNumber,
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
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
