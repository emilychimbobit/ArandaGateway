using ArandaGateway.Api.Application.Equipos;
using ArandaGateway.Api.Contracts.Equipos;
using ArandaGateway.Api.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArandaGateway.Api.Endpoints;

public static class EquiposEndpoints
{
    public static IEndpointRouteBuilder MapEquiposEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/equipos")
            .WithTags("Equipos");

        group
            .MapGet("/", ListAssignedEquiposAsync)
            .WithName("ListAssignedEquipos")
            .WithSummary("Revisión de equipos asignados al colaborador")
            .WithDescription("Implementa REQ_10. Consulta en CMDB los equipos asociados al colaborador autenticado.")
            .Produces<IReadOnlyList<EquipoResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

   private static async Task<IResult> ListAssignedEquiposAsync(
        [FromHeader(Name = HeaderCurrentCollaborator.HeaderName)]
        string? collaboratorUsername,
        IEquipoService equipoService,
        CancellationToken cancellationToken)
    {
        var result = await equipoService.ListAssignedEquiposAsync(cancellationToken);

        return result.Status switch
        {
            EquipoOperationResultStatus.Success => 
                Results.Ok(result.Value),

            EquipoOperationResultStatus.NoRecordsFound => 
                Results.Ok(new { mensaje = "Sin registros asociados", total = 0, data = Array.Empty<EquipoResponse>() }),

            EquipoOperationResultStatus.MissingCollaborator => 
                MissingCollaboratorProblem(),

            EquipoOperationResultStatus.SourceUnavailable => 
                Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Fuente no disponible.",
                    detail: result.Error),

            _ => throw new InvalidOperationException($"Resultado no soportado: {result.Status}.")
        };
    }

    private static IResult MissingCollaboratorProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "No se pudo identificar al colaborador.",
            detail: $"Envíe el encabezado {HeaderCurrentCollaborator.HeaderName}.");
}