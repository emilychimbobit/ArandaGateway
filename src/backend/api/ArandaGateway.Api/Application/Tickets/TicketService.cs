using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Application.Tickets;

public sealed class TicketService(
    ICurrentCollaborator currentCollaborator,
    IArandaClient arandaClient,
    IOptions<ArandaOptions> options,
    ILogger<TicketService>? logger = null) : ITicketService
{
    /// <summary>
    /// Estados anulables del DEF, con los nombres reales del modelo 17 leídos
    /// de <c>api/v9/model/17/4/states</c>: Registrado (59), Asignado (60) y
    /// En Proceso (65). El DEF los enuncia como "Registrado/Asignado" y
    /// "En proceso", pero en Aranda los dos primeros son estados separados.
    /// Los tres estados en pausa —En Aprobacion (62), Pendiente usuario (63) y
    /// Pendiente proveedor (64)— quedan fuera porque el DEF los declara no
    /// anulables.
    /// </summary>
    private static readonly HashSet<string> AllowedCancellationStates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Registrado",
            "Asignado",
            "En Proceso"
        };

    /// <summary>
    /// Estados en los que un ticket ya terminó: Cancelado (61), Cerrado (67) y
    /// Resuelto (66). No sirve ninguna bandera de Aranda para deducirlos:
    /// Resuelto llega con <c>isClosed = false</c> y también con
    /// <c>isFinal = false</c>, porque desde ahí se puede volver a En Proceso.
    /// Pendiente de confirmación del cliente si Resuelto debe seguir visible
    /// para el colaborador.
    /// </summary>
    private static readonly HashSet<string> ClosedStates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Resuelto",
            "Cerrado",
            "Cancelado"
        };

    /// <summary>
    /// Aranda acepta asuntos de hasta 400 caracteres. Con 401 responde
    /// <c>500</c> con <c>FailureAddItem</c>: no trunca ni dice qué campo
    /// sobra. Medido el 16 de septiembre de 2026 contra el entorno real,
    /// acotando por bisección entre 313 y 4000; 400 se guarda intacto y 401
    /// falla. La descripción no necesita tope: 100 000 caracteres vuelven
    /// completos.
    /// </summary>
    private const int MaxSubjectLength = 400;

    /// <summary>
    /// Escapa lo que hace daño en HTML y nada más.
    /// <see cref="HtmlEncoder.Default"/> escapa además todo lo que no sea
    /// ASCII, así que cada tilde ocupaba seis caracteres (<c>&amp;#xF3;</c>) y
    /// Mesa de Ayuda las veía crudas en la consola. Con
    /// <see cref="UnicodeRanges.Latin1Supplement"/> permitido el texto en
    /// español viaja tal cual, y el asunto deja de inflarse contra el límite
    /// de <see cref="MaxSubjectLength"/>.
    /// </summary>
    private static readonly HtmlEncoder TextEncoder = HtmlEncoder.Create(
        new TextEncoderSettings(
            UnicodeRanges.BasicLatin,
            UnicodeRanges.Latin1Supplement));

    /// <summary>
    /// Extensiones que admite el adjunto, según el REQ_04. Se guardan
    /// ordenadas porque además de filtrar se publican en
    /// <see cref="GetCreationParameters"/>: el orden alfabético las hace
    /// estables en el resumen aunque se agregue una nueva.
    /// </summary>
    private static readonly string[] AllowedExtensions =
    [
        ".docx",
        ".jpg",
        ".pdf",
        ".png",
        ".ppt",
        ".xlsx"
    ];

    private readonly ArandaOptions arandaOptions = options.Value;

    public async Task<TicketOperationResult<RespuestaCrearTicket>>
        CreateTicketAsync(
            SolicitudCrearTicket request,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<RespuestaCrearTicket>();
        }

        if (string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return Invalid<RespuestaCrearTicket>(
                "El asunto y la descripción son obligatorios.");
        }

        // Los límites se miden sobre lo que realmente viaja: con el prefijo
        // puesto y ya codificado. Medirlos sobre el texto del colaborador
        // dejaría pasar asuntos que Aranda rechaza con un 500 sin explicación.
        var subject = TextEncoder.Encode(
            ApplySubjectPrefix(request.Subject.Trim()));
        if (subject.Length > MaxSubjectLength)
        {
            return Invalid<RespuestaCrearTicket>(
                $"El asunto supera el máximo de {MaxSubjectLength} " +
                "caracteres que acepta Aranda.");
        }

        var description = TextEncoder.Encode(request.Description.Trim());
        if (description.Length > arandaOptions.MaxDescriptionLength)
        {
            return Invalid<RespuestaCrearTicket>(
                "La descripción supera el máximo de " +
                $"{arandaOptions.MaxDescriptionLength} caracteres.");
        }

        if (!TryGetTypeConfiguration(request.Type, out var configuration))
        {
            return ConfigurationMissing<RespuestaCrearTicket>();
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return NotFoundOrNotOwned<RespuestaCrearTicket>();
        }

        var created = await arandaClient.CreateTicketAsync(
            new ArandaCreateTicketRequest
            {
                CategoryId = configuration.CategoryId,
                CustomerId = user.Id,
                ApplicantId = user.Id,
                Description = description,
                ItemType = configuration.ItemType,
                ImpactId = configuration.ImpactId,
                UrgencyId = configuration.UrgencyId,
                ModelId = configuration.ModelId,
                ProjectId = arandaOptions.ProjectId,
                RegistryTypeId = configuration.RegistryTypeId,
                UnitId = configuration.UnitId,
                ServiceId = configuration.ServiceId,
                StateId = configuration.InitialStateId,
                AuthorId = arandaOptions.AuthorId,
                GroupId = configuration.GroupId,
                Subject = subject
            },
            cancellationToken);

        return Success(
            new RespuestaCrearTicket(
                created.IdByProject,
                "Creado"));
    }

    public RespuestaParametrosCreacion GetCreationParameters()
    {
        var clasificacion = arandaOptions.Classification;
        var prefijo = arandaOptions.SubjectPrefix?.Trim();

        return new(
            new RespuestaClasificacionTicket(
                clasificacion.Service,
                clasificacion.Impact,
                clasificacion.Urgency,
                clasificacion.Category,
                clasificacion.Group),
            // Sin prefijo configurado el asunto va tal cual, así que el
            // resumen tiene que decir que no hay ninguno, no una cadena vacía.
            string.IsNullOrEmpty(prefijo) ? null : prefijo,
            new RespuestaLimitesTicket(
                MaxSubjectLength,
                arandaOptions.MaxDescriptionLength,
                arandaOptions.MaxAttachmentBytes,
                AllowedExtensions));
    }

    public async Task<
        TicketOperationResult<IReadOnlyList<RespuestaResumenTicket>>>
        ListOpenTicketsAsync(CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<
                IReadOnlyList<RespuestaResumenTicket>>();
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return NotFoundOrNotOwned<
                IReadOnlyList<RespuestaResumenTicket>>();
        }

        var search = await arandaClient.SearchTicketsAsync(
            BuildCollaboratorTicketsSearch(user.Id),
            cancellationToken);

        var tickets = search.Content
            .Where(ticket =>
                (ticket.CustomerId is null ||
                    ticket.CustomerId == user.Id) &&
                IsOpen(ticket) &&
                ticket.IdByProject is not null &&
                ticket.Subject is not null &&
                ticket.StateName is not null &&
                ticket.OpenedDate is not null)
            .Select(ticket => new RespuestaResumenTicket(
                ticket.IdByProject!,
                ticket.Subject!,
                ticket.StateName!,
                DateTimeOffset.FromUnixTimeMilliseconds(
                    ticket.OpenedDate!.Value)))
            .ToArray();

        return Success<IReadOnlyList<RespuestaResumenTicket>>(tickets);
    }

    public async Task<TicketDetailResult> GetTicketDetailAsync(
        string caseNumber,
        CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return new(TicketDetailResultStatus.MissingCollaborator);
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);

        if (ticket is null)
        {
            return new(TicketDetailResultStatus.NotFoundOrNotOwned);
        }

        if (ticket.IdByProject is null ||
            ticket.StateName is null ||
            ticket.ModifiedDate is null)
        {
            throw new ArandaContractException(
                "Aranda returned an incomplete ticket.");
        }

        return new(
            TicketDetailResultStatus.Success,
            new RespuestaDetalleTicket(
                ticket.IdByProject,
                ticket.StateName,
                ticket.GroupName,
                DateTimeOffset.FromUnixTimeMilliseconds(
                    ticket.ModifiedDate.Value),
                null));
    }

    public async Task<TicketOperationResult<RespuestaAnularTicket>>
        CancelTicketAsync(
            string caseNumber,
            SolicitudAnularTicket request,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<RespuestaAnularTicket>();
        }

        if (!request.Confirmed)
        {
            return Invalid<RespuestaAnularTicket>(
                "La anulación requiere confirmación explícita.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Invalid<RespuestaAnularTicket>(
                "El motivo de anulación es obligatorio.");
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);
        if (ticket is null)
        {
            return NotFoundOrNotOwned<RespuestaAnularTicket>();
        }

        if (ticket.StateName is null ||
            !AllowedCancellationStates.Contains(ticket.StateName))
        {
            return new(
                TicketOperationResultStatus.InvalidState,
                Error:
                    "El ticket no se encuentra en un estado anulable.");
        }

        var cancellationStateId = ticket.ItemType switch
        {
            1 => arandaOptions.IncidentCancellationStateId,
            4 => arandaOptions.ServiceRequestCancellationStateId,
            _ => null
        };
        if (!IsPositive(cancellationStateId))
        {
            return ConfigurationMissing<RespuestaAnularTicket>();
        }

        var registryTypeId = IsPositive(ticket.RegistryTypeId)
            ? ticket.RegistryTypeId
            : arandaOptions.RegistryTypeId;
        if (!IsPositive(registryTypeId))
        {
            return ConfigurationMissing<RespuestaAnularTicket>();
        }

        var update = await arandaClient.UpdateTicketAsync(
            ticket.Id,
            new ArandaUpdateTicketRequest
            {
                CategoryId = ticket.CategoryId,
                ItemType = ticket.ItemType,
                ItemVersion = ticket.ItemVersion ?? 0,
                ModelId = ticket.ModelId,
                ProjectId = ticket.ProjectId,
                RegistryTypeId = registryTypeId!.Value,
                ServiceId = ticket.ServiceId,
                StateId = cancellationStateId!.Value,
                Commentary = TextEncoder.Encode(
                    request.Reason.Trim())
            },
            cancellationToken);

        if (!update.Result)
        {
            throw new ArandaContractException(
                "Aranda did not confirm the ticket cancellation.");
        }

        return Success(
            new RespuestaAnularTicket(
                ticket.IdByProject ?? caseNumber.Trim(),
                "Anulado"));
    }

    public async Task<TicketOperationResult<RespuestaAdjuntarArchivo>>
        UploadAttachmentAsync(
            string caseNumber,
            TicketAttachment attachment,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<RespuestaAdjuntarArchivo>();
        }

        var fileName = Path.GetFileName(attachment.FileName);
        var extension = Path.GetExtension(fileName);
        var contentType = attachment.ContentType;
        if (string.IsNullOrWhiteSpace(extension) &&
            TryInferImageFormat(attachment.Content, out var inferredExtension,
                out var inferredContentType))
        {
            fileName += inferredExtension;
            extension = inferredExtension;
            contentType = inferredContentType;
        }

        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Any(char.IsControl) ||
            !AllowedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
        {
            return Invalid<RespuestaAdjuntarArchivo>(
                "El formato del archivo no está permitido.");
        }

        if (attachment.Length is <= 0 ||
            attachment.Length > arandaOptions.MaxAttachmentBytes)
        {
            return Invalid<RespuestaAdjuntarArchivo>(
                "El archivo supera el límite configurado.");
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);
        if (ticket is null)
        {
            return NotFoundOrNotOwned<RespuestaAdjuntarArchivo>();
        }

        var uploadResults = await arandaClient.UploadAttachmentAsync(
            new ArandaAttachmentUpload(
                ticket.Id,
                ticket.ItemType,
                fileName,
                contentType,
                attachment.Content,
                attachment.Description),
            cancellationToken);

        var result = uploadResults.SingleOrDefault();
        if (result is null || !result.Result)
        {
            throw new ArandaContractException(
                "Aranda did not confirm the file upload.");
        }

        return Success(
            new RespuestaAdjuntarArchivo(result.FileName, true));
    }

    private static bool TryInferImageFormat(
        Stream content,
        out string extension,
        out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;
        if (!content.CanSeek)
        {
            return false;
        }

        var originalPosition = content.Position;
        Span<byte> header = stackalloc byte[8];
        var bytesRead = content.Read(header);
        content.Position = originalPosition;

        if (bytesRead >= 8 &&
            header[0] == 137 &&
            header[1] == 80 &&
            header[2] == 78 &&
            header[3] == 71 &&
            header[4] == 13 &&
            header[5] == 10 &&
            header[6] == 26 &&
            header[7] == 10)
        {
            extension = ".png";
            contentType = "image/png";
            return true;
        }

        if (bytesRead >= 3 &&
            header[0] == 255 &&
            header[1] == 216 &&
            header[2] == 255)
        {
            extension = ".jpg";
            contentType = "image/jpeg";
            return true;
        }

        return false;
    }

    private async Task<ArandaUser?> ResolveActiveUserAsync(
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await arandaClient.GetUserByUsernameAsync(
                username,
                cancellationToken);
            return user.IsActive ? user : null;
        }
        catch (ArandaApiException exception) when (exception.IsUserNotFound())
        {
            return null;
        }
    }

    private ArandaSearchTicketsRequest BuildCollaboratorTicketsSearch(
        long customerId) =>
        new()
        {
            Criteria =
            [
                new ArandaSearchCriterion
                {
                    FieldName = "customerId",
                    FieldValue = "customerId",
                    OperatorName = "eq",
                    OperatorValue = "==",
                    Value = customerId,
                    ValueName = customerId,
                    Type = 6
                }
            ],
            PageIndex = 0,
            PageSize = arandaOptions.SearchPageSize,
            Projects =
            [
                new ArandaProjectFilter(arandaOptions.ProjectId)
            ],
            Repository = 3,
            Types =
            [
                new ArandaItemTypeFilter(1),
                new ArandaItemTypeFilter(2),
                new ArandaItemTypeFilter(3),
                new ArandaItemTypeFilter(4)
            ]
        };

    // El consumidor solo conoce el número de caso (idByProject). Aranda
    // consulta el detalle por su identificador interno, así que el caso se
    // resuelve dentro de los tickets del propio colaborador: eso traduce el
    // identificador y confirma la propiedad en un solo paso.
    private async Task<ArandaTicket?> ResolveOwnedTicketAsync(
        string caseNumber,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
        {
            return null;
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var search = await arandaClient.SearchTicketsAsync(
            BuildCollaboratorTicketsSearch(user.Id),
            cancellationToken);

        var match = search.Content.FirstOrDefault(ticket =>
            string.Equals(
                ticket.IdByProject?.Trim(),
                caseNumber.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        // La propiedad se verifica contra el usuario resuelto en Aranda, no
        // contra la cabecera.
        return await GetOwnedTicketOrNullAsync(
            match.Id,
            user.UserName,
            cancellationToken);
    }

    private async Task<ArandaTicket?> GetOwnedTicketOrNullAsync(
        long caseNumber,
        string ownerUserName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetOwnedTicketAsync(
                caseNumber,
                ownerUserName,
                cancellationToken);
        }
        catch (ArandaApiException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<ArandaTicket?> GetOwnedTicketAsync(
        long caseNumber,
        string ownerUserName,
        CancellationToken cancellationToken)
    {
        var ticket = await arandaClient.GetTicketAsync(
            caseNumber,
            cancellationToken);

        return string.Equals(
            ticket.CustomerUserName,
            ownerUserName,
            StringComparison.OrdinalIgnoreCase)
            ? ticket
            : null;
    }

    private bool TryGetTypeConfiguration(
        TicketKind type,
        out TicketTypeConfiguration configuration)
    {
        var modelId = type switch
        {
            TicketKind.Incident => arandaOptions.IncidentModelId,
            TicketKind.ServiceRequest =>
                arandaOptions.ServiceRequestModelId,
            _ => null
        };
        var initialStateId = type switch
        {
            TicketKind.Incident =>
                arandaOptions.IncidentInitialStateId,
            TicketKind.ServiceRequest =>
                arandaOptions.ServiceRequestInitialStateId,
            _ => null
        };
        var itemType = type switch
        {
            TicketKind.Incident => 1,
            TicketKind.ServiceRequest => 4,
            _ => 0
        };

        if (itemType == 0 ||
            !IsPositive(modelId) ||
            !IsPositive(initialStateId) ||
            !IsPositive(arandaOptions.CategoryId) ||
            !IsPositive(arandaOptions.ServiceId) ||
            !IsPositive(arandaOptions.ImpactId) ||
            !IsPositive(arandaOptions.UrgencyId) ||
            !IsPositive(arandaOptions.GroupId) ||
            !IsPositive(arandaOptions.RegistryTypeId))
        {
            configuration = default;
            return false;
        }

        configuration = new(
            itemType,
            modelId!.Value,
            initialStateId!.Value,
            arandaOptions.CategoryId!.Value,
            arandaOptions.ServiceId!.Value,
            arandaOptions.ImpactId!.Value,
            arandaOptions.UrgencyId!.Value,
            arandaOptions.GroupId!.Value,
            arandaOptions.RegistryTypeId!.Value,
            // La unidad organizacional es opcional: Aranda rechaza con
            // InvalidOrganizationArea una unidad que no corresponde al
            // cliente, y sin el dato resuelve el área por su cuenta.
            IsPositive(arandaOptions.UnitId) ? arandaOptions.UnitId : null);
        return true;
    }

    /// <summary>
    /// Un ticket sigue abierto si Aranda no lo cerró y su estado no es
    /// terminal. Se excluye por lista de estados terminados en lugar de
    /// aceptar una lista de estados en curso: así un estado nuevo de Aranda
    /// aparece en el listado en vez de desaparecer sin aviso.
    /// </summary>
    private static bool IsOpen(ArandaTicket ticket) =>
        !ticket.IsClosed &&
        (ticket.StateName is null ||
            !ClosedStates.Contains(ticket.StateName.Trim()));

    /// <summary>
    /// Marca el asunto con <c>Aranda:SubjectPrefix</c> cuando está configurado,
    /// para que Mesa de Ayuda reconozca los tickets del bot. Sin prefijo
    /// configurado el asunto no se toca, y si el colaborador ya lo escribió no
    /// se repite.
    /// </summary>
    private string ApplySubjectPrefix(string subject)
    {
        var prefix = arandaOptions.SubjectPrefix?.Trim();
        if (string.IsNullOrEmpty(prefix) ||
            subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return subject;
        }

        return $"{prefix} {subject}";
    }

    private static bool IsPositive(long? value) => value is > 0;

    private static TicketOperationResult<T> Success<T>(T value) =>
        new(TicketOperationResultStatus.Success, value);

    private static TicketOperationResult<T> MissingCollaborator<T>() =>
        new(
            TicketOperationResultStatus.MissingCollaborator,
            Error: "No se pudo identificar al colaborador.");

    private static TicketOperationResult<T> Invalid<T>(string error) =>
        new(TicketOperationResultStatus.InvalidRequest, Error: error);

    private static TicketOperationResult<T> NotFoundOrNotOwned<T>() =>
        new(TicketOperationResultStatus.NotFoundOrNotOwned);

    private static TicketOperationResult<T> ConfigurationMissing<T>() =>
        new(
            TicketOperationResultStatus.ConfigurationMissing,
            Error:
                "La operación requiere configuración adicional de Aranda.");

    private readonly record struct TicketTypeConfiguration(
        long ItemType,
        long ModelId,
        long InitialStateId,
        long CategoryId,
        long ServiceId,
        long ImpactId,
        long UrgencyId,
        long GroupId,
        long RegistryTypeId,
        long? UnitId);
}
