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
            .WithTags("Tickets")
            .RequireAuthorization();

        group
            .MapPost("/", CreateTicketAsync)
            .WithName("CreateTicket")
            .WithSummary("Crea un incidente o requerimiento en Aranda")
            .Produces<CreateTicketResponse>(
                StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group
            .MapGet("/", ListOpenTicketsAsync)
            .WithName("ListOpenTickets")
            .WithSummary("Lista los tickets abiertos del colaborador")
            .Produces<IReadOnlyList<TicketSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group
            .MapGet("/{caseNumber:long}", GetTicketDetailAsync)
            .WithName("GetTicketDetail")
            .WithSummary("Consulta el estado de un ticket propio")
            .WithDescription(
                "Implementa REQ_07. Requiere temporalmente el username " +
                "del propietario en X-Collaborator-Username.")
            .Produces<TicketDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group
            .MapPost(
                "/{caseNumber:long}/cancellation",
                CancelTicketAsync)
            .WithName("CancelTicket")
            .WithSummary("Anula un ticket propio")
            .Produces<CancelTicketResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group
            .MapPost(
                "/{caseNumber:long}/attachments",
                UploadAttachmentAsync)
            .WithName("UploadTicketAttachment")
            .WithSummary("Adjunta un archivo a un ticket propio")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadAttachmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> CreateTicketAsync(
        CreateTicketRequest request,
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
        ITicketService ticketService,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CreateTicketAsync(
            request,
            cancellationToken);

        return MapOperationResult(
            result,
            value => Results.Json(
                value,
                statusCode: StatusCodes.Status201Created));
    }

    private static async Task<IResult> ListOpenTicketsAsync(
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
        ITicketService ticketService,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.ListOpenTicketsAsync(
            cancellationToken);
        return MapOperationResult(result, Results.Ok);
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
                MissingCollaboratorProblem(),
            TicketDetailResultStatus.NotFoundOrNotOwned =>
                Results.NotFound(),
            _ => throw new InvalidOperationException(
                $"Unsupported ticket result: {result.Status}.")
        };
    }

    private static async Task<IResult> CancelTicketAsync(
        long caseNumber,
        CancelTicketRequest request,
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
        ITicketService ticketService,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CancelTicketAsync(
            caseNumber,
            request,
            cancellationToken);
        return MapOperationResult(result, Results.Ok);
    }

    private static async Task<IResult> UploadAttachmentAsync(
        long caseNumber,
        IFormFile file,
        [FromForm] string? description,
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
        ITicketService ticketService,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var attachment = new TicketAttachment(
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            file.Length,
            content,
            description);

        var result = await ticketService.UploadAttachmentAsync(
            caseNumber,
            attachment,
            cancellationToken);
        return MapOperationResult(result, Results.Ok);
    }

    private static IResult MapOperationResult<T>(
        TicketOperationResult<T> result,
        Func<T, IResult> success)
    {
        return result.Status switch
        {
            TicketOperationResultStatus.Success
                when result.Value is not null =>
                success(result.Value),
            TicketOperationResultStatus.MissingCollaborator =>
                MissingCollaboratorProblem(),
            TicketOperationResultStatus.InvalidRequest =>
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "La solicitud no es válida.",
                    detail: result.Error),
            TicketOperationResultStatus.NotFoundOrNotOwned =>
                Results.NotFound(),
            TicketOperationResultStatus.InvalidState =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "El ticket no puede anularse.",
                    detail: result.Error),
            TicketOperationResultStatus.ConfigurationMissing =>
                Results.Problem(
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable,
                    title: "La integración no está configurada.",
                    detail: result.Error),
            _ => throw new InvalidOperationException(
                $"Unsupported ticket result: {result.Status}.")
        };
    }

    private static IResult MissingCollaboratorProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "No se pudo identificar al colaborador.",
            detail:
                $"Envíe el encabezado {HeaderCurrentCollaborator.HeaderName}.");
}
