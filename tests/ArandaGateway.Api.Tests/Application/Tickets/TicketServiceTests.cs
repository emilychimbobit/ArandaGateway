using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Tests.Application.Tickets;

public sealed class TicketServiceTests
{
    [Fact]
    public async Task GetTicketDetailAsync_ReturnsOwnedTicket()
    {
        var client = CreateClientWithOwnedTicket();
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            "CASE-154",
            CancellationToken.None);

        Assert.Equal(TicketDetailResultStatus.Success, result.Status);
        Assert.Equal("CASE-154", result.Ticket?.CaseNumber);
        Assert.Null(result.Ticket?.Solution);
    }

    [Fact]
    public async Task GetTicketDetailAsync_ResolvesInternalIdFromCaseNumber()
    {
        var client = CreateClientWithOwnedTicket();
        var service = CreateService(client);

        await service.GetTicketDetailAsync(
            "case-154",
            CancellationToken.None);

        Assert.Equal(154, client.LastTicketId);
    }

    [Fact]
    public async Task GetTicketDetailAsync_HidesCaseNumberOfAnotherUser()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = EmptySearchResult()
        };
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            "CASE-999",
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.NotFoundOrNotOwned,
            result.Status);
        Assert.Null(result.Ticket);
        Assert.Null(client.LastTicketId);
    }

    [Fact]
    public async Task GetTicketDetailAsync_HidesTicketOwnedByAnotherUser()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(CreateTicket("another-user")),
            Ticket = CreateTicket("another-user")
        };
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            "CASE-154",
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.NotFoundOrNotOwned,
            result.Status);
        Assert.Null(result.Ticket);
    }

    [Fact]
    public async Task GetTicketDetailAsync_RequiresCollaborator()
    {
        var service = CreateService(
            new StubArandaClient(),
            username: null);

        var result = await service.GetTicketDetailAsync(
            "CASE-154",
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.MissingCollaborator,
            result.Status);
    }

    [Fact]
    public async Task GetTicketDetailAsync_RejectsBlankCaseNumber()
    {
        var client = new StubArandaClient();
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            "   ",
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.NotFoundOrNotOwned,
            result.Status);
        Assert.Null(client.LastSearchRequest);
    }

    [Fact]
    public async Task CreateTicketAsync_MapsConfiguredValues()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            CreatedTicket = new()
            {
                Id = 200,
                IdByProject = "RF-200"
            }
        };
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "  Subject  ",
                "  Description  "),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal("RF-200", result.Value?.CaseNumber);
        Assert.Equal(4, client.LastCreateRequest?.ItemType);
        Assert.Equal(1, client.LastCreateRequest?.ProjectId);
        Assert.Equal(2, client.LastCreateRequest?.AuthorId);
        Assert.Equal(10, client.LastCreateRequest?.CustomerId);
        Assert.Equal(9, client.LastCreateRequest?.UnitId);
        Assert.Equal("Subject", client.LastCreateRequest?.Subject);
    }

    /// <summary>
    /// El agente arma el resumen de confirmacion con esta clasificacion en vez
    /// de quemarla en el topic, para que no se desincronice si cambia la
    /// configuracion del gateway. Son los cinco valores fijos del REQ_04.
    /// </summary>
    [Fact]
    public void GetCreationParameters_ReturnsTheConfiguredClassification()
    {
        var service = CreateService(new StubArandaClient());

        var clasificacion = service.GetCreationParameters().Clasificacion;

        Assert.Equal("Por categorizar", clasificacion.Servicio);
        Assert.Equal("Bajo", clasificacion.Impacto);
        Assert.Equal("Bajo", clasificacion.Urgencia);
        Assert.Equal("Ticket creado por bot", clasificacion.Categoria);
        Assert.Equal("Mesa de Ayuda", clasificacion.Grupo);
    }

    [Fact]
    public void GetCreationParameters_HonoursOverriddenNames()
    {
        var service = CreateService(
            new StubArandaClient(),
            options: CreateOptions(
                classification: new()
                {
                    Service = "Otro servicio",
                    Group = "Otro grupo"
                }));

        var clasificacion = service.GetCreationParameters().Clasificacion;

        Assert.Equal("Otro servicio", clasificacion.Servicio);
        Assert.Equal("Otro grupo", clasificacion.Grupo);
    }

    /// <summary>
    /// El resumen previo tiene que mostrar el asunto tal como quedara
    /// registrado, prefijo incluido: sin el dato el colaborador confirma un
    /// asunto distinto del que ve Mesa de Ayuda.
    /// </summary>
    [Fact]
    public void GetCreationParameters_ReturnsTheConfiguredSubjectPrefix()
    {
        var service = CreateService(
            new StubArandaClient(),
            options: CreateOptions(subjectPrefix: "[PRUEBA BOT]"));

        Assert.Equal(
            "[PRUEBA BOT]",
            service.GetCreationParameters().PrefijoAsunto);
    }

    [Fact]
    public void GetCreationParameters_WithoutSubjectPrefix_ReturnsNull()
    {
        var service = CreateService(new StubArandaClient());

        Assert.Null(service.GetCreationParameters().PrefijoAsunto);
    }

    /// <summary>
    /// Los limites viajan en el resumen para que el agente recorte antes de
    /// llamar a la creacion, en vez de descubrirlos con un 400.
    /// </summary>
    [Fact]
    public void GetCreationParameters_ReturnsTheLimitsCreationEnforces()
    {
        var service = CreateService(
            new StubArandaClient(),
            options: CreateOptions(
                maxDescriptionLength: 1_500,
                maxAttachmentBytes: 1_048_576));

        var limites = service.GetCreationParameters().Limites;

        Assert.Equal(400, limites.MaxAsunto);
        Assert.Equal(1_500, limites.MaxDescripcion);
        Assert.Equal(1_048_576, limites.MaxBytesAdjunto);
        Assert.Equal(
            [".docx", ".jpg", ".pdf", ".png", ".ppt", ".xlsx"],
            limites.ExtensionesPermitidas);
    }

    /// <summary>
    /// La creacion ya no repite la clasificacion: vive en
    /// <c>GET /api/tickets/parametros-creacion</c>, que el agente consulta para
    /// el resumen previo a la confirmacion.
    /// </summary>
    [Fact]
    public async Task CreateTicketAsync_ReturnsOnlyTheCaseAndItsStatus()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(TicketKind.ServiceRequest, "Subject", "Description"),
            CancellationToken.None);

        Assert.Equal(new RespuestaCrearTicket("RF-200", "Creado"), result.Value);
    }

    [Fact]
    public async Task CreateTicketAsync_WithSubjectPrefix_PrependsIt()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            CreatedTicket = new()
            {
                Id = 200,
                IdByProject = "RF-200"
            }
        };
        var service = CreateService(
            client,
            options: CreateOptions(subjectPrefix: "[PRUEBA BOT]"));

        await service.CreateTicketAsync(
            new(TicketKind.ServiceRequest, "  Subject  ", "Description"),
            CancellationToken.None);

        Assert.Equal(
            "[PRUEBA BOT] Subject",
            client.LastCreateRequest?.Subject);
    }

    [Fact]
    public async Task CreateTicketAsync_WithSubjectPrefix_DoesNotRepeatIt()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            CreatedTicket = new()
            {
                Id = 200,
                IdByProject = "RF-200"
            }
        };
        var service = CreateService(
            client,
            options: CreateOptions(subjectPrefix: "[PRUEBA BOT]"));

        await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "[prueba bot] Subject",
                "Description"),
            CancellationToken.None);

        Assert.Equal(
            "[prueba bot] Subject",
            client.LastCreateRequest?.Subject);
    }

    [Fact]
    public async Task CreateTicketAsync_KeepsAccentsUnescaped()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Configuración de contraseña",
                "No puedo acceder al módulo de facturación"),
            CancellationToken.None);

        Assert.Equal(
            "Configuración de contraseña",
            client.LastCreateRequest?.Subject);
        Assert.Equal(
            "No puedo acceder al módulo de facturación",
            client.LastCreateRequest?.Description);
    }

    [Fact]
    public async Task CreateTicketAsync_StillEscapesMarkup()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Falla <b>grave</b>",
                "Error en a & b"),
            CancellationToken.None);

        Assert.Equal(
            "Falla &lt;b&gt;grave&lt;/b&gt;",
            client.LastCreateRequest?.Subject);
        Assert.Equal(
            "Error en a &amp; b",
            client.LastCreateRequest?.Description);
    }

    [Fact]
    public async Task CreateTicketAsync_AcceptsSubjectAtArandaLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                new string('a', 400),
                "Description"),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal(400, client.LastCreateRequest?.Subject.Length);
    }

    [Fact]
    public async Task CreateTicketAsync_RejectsSubjectOverArandaLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                new string('a', 401),
                "Description"),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Contains("400", result.Error);
        Assert.Null(client.LastCreateRequest);
    }

    /// <summary>
    /// El límite de Aranda se mide sobre el asunto ya marcado con el prefijo,
    /// que es lo que viaja: sin contarlo, un asunto al borde pasaría la
    /// validación y Aranda lo rechazaría con 500 FailureAddItem.
    /// </summary>
    [Fact]
    public async Task CreateTicketAsync_CountsSubjectPrefixAgainstTheLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(
            client,
            options: CreateOptions(subjectPrefix: "[PRUEBA BOT]"));

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                new string('a', 400 - "[PRUEBA BOT] ".Length + 1),
                "Description"),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Null(client.LastCreateRequest);
    }

    /// <summary>
    /// El escape multiplica por cuatro o más cada carácter marcado, así que el
    /// límite se mide sobre el texto ya codificado y no sobre el que escribió
    /// el colaborador: 150 caracteres —el máximo del formulario— pasan a ocupar
    /// 600 y Aranda los rechazaría con 500.
    /// </summary>
    [Fact]
    public async Task CreateTicketAsync_MeasuresTheLimitAfterEncoding()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                new string('<', 150),
                "Description"),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Null(client.LastCreateRequest);
    }

    [Fact]
    public async Task CreateTicketAsync_AcceptsDescriptionAtConfiguredLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Subject",
                new string('a', 20_000)),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal(20_000, client.LastCreateRequest?.Description.Length);
    }

    [Fact]
    public async Task CreateTicketAsync_RejectsDescriptionOverConfiguredLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(client);

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Subject",
                new string('a', 20_001)),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Contains("20000", result.Error);
        Assert.Null(client.LastCreateRequest);
    }

    /// <summary>
    /// El tope de la descripción es política del gateway, no un límite de
    /// Aranda —que acepta 100 000 caracteres—, así que se configura.
    /// </summary>
    [Fact]
    public async Task CreateTicketAsync_HonoursConfiguredDescriptionLimit()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(
            client,
            options: CreateOptions(maxDescriptionLength: 100));

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Subject",
                new string('a', 101)),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Contains("100", result.Error);
        Assert.Null(client.LastCreateRequest);
    }

    /// <summary>
    /// Igual que el asunto: el escape puede multiplicar cada carácter, así que
    /// el tope se mide sobre el texto ya codificado.
    /// </summary>
    [Fact]
    public async Task CreateTicketAsync_MeasuresDescriptionLimitAfterEncoding()
    {
        var client = CreateClientReadyToCreate();
        var service = CreateService(
            client,
            options: CreateOptions(maxDescriptionLength: 100));

        var result = await service.CreateTicketAsync(
            new(
                TicketKind.ServiceRequest,
                "Subject",
                new string('<', 30)),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Null(client.LastCreateRequest);
    }

    [Fact]
    public async Task CancelTicketAsync_KeepsAccentsUnescapedInReason()
    {
        var client = CreateClientWithOwnedTicket(
            updateResult: new() { ItemVersion = 2, Result = true });
        var service = CreateService(client);

        await service.CancelTicketAsync(
            "CASE-154",
            new("Ya se resolvió solo", true),
            CancellationToken.None);

        Assert.Equal(
            "Ya se resolvió solo",
            client.LastUpdateRequest?.Commentary);
    }

    [Fact]
    public async Task CreateTicketAsync_FailsWhenCatalogsAreMissing()
    {
        var service = CreateService(
            new StubArandaClient(),
            options: new ArandaOptions
            {
                BaseUrl = new("https://aranda.example/"),
                ApiKey = "Bearer test",
                ProjectId = 1,
                AuthorId = 2
            });

        var result = await service.CreateTicketAsync(
            new(TicketKind.Incident, "Subject", "Description"),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.ConfigurationMissing,
            result.Status);
    }

    [Fact]
    public async Task ListOpenTicketsAsync_ReturnsOnlyOwnedOpenTickets()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = new()
            {
                Content =
                [
                    CreateTicket("collaborator"),
                    CreateTicket("other") with { CustomerId = 20 },
                    CreateTicket("collaborator") with
                    {
                        Id = 155,
                        IsClosed = true
                    }
                ],
                TotalItems = 3,
                TotalPage = 1
            }
        };
        var service = CreateService(client);

        var result = await service.ListOpenTicketsAsync(
            CancellationToken.None);

        var ticket = Assert.Single(result.Value!);
        Assert.Equal("CASE-154", ticket.CaseNumber);
        Assert.Equal(3, client.LastSearchRequest?.Repository);
        Assert.Equal(0, client.LastSearchRequest?.PageIndex);
        Assert.Equal([1L, 2L, 3L, 4L],
            client.LastSearchRequest?.Types.Select(type => type.ItemType));
        var criterion = Assert.Single(client.LastSearchRequest!.Criteria);
        Assert.Equal("customerId", criterion.FieldName);
        Assert.Equal("eq", criterion.OperatorName);
        Assert.Equal("==", criterion.OperatorValue);
        Assert.Equal(10, criterion.Value);
        Assert.Equal(10, criterion.ValueName);
    }

    [Fact]
    public async Task ListOpenTicketsAsync_MapsSearchItemsWithoutCustomerId()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = new()
            {
                Content =
                [
                    CreateTicket("UE BIT 20") with
                    {
                        Id = 50518,
                        IdByProject = "RF-50518",
                        CustomerId = null,
                        Subject = "Solicitud de acceso a Microsoft Teams",
                        StateId = 59,
                        StateName = "En proceso",
                        OpenedDate = 1786568146563
                    }
                ],
                TotalItems = 1,
                TotalPage = 1
            }
        };
        var service = CreateService(client);

        var result = await service.ListOpenTicketsAsync(
            CancellationToken.None);

        var ticket = Assert.Single(result.Value!);
        Assert.Equal("RF-50518", ticket.CaseNumber);
        Assert.Equal(
            "Solicitud de acceso a Microsoft Teams",
            ticket.Subject);
        Assert.Equal("En proceso", ticket.Status);
    }

    /// <summary>
    /// Aranda solo marca isClosed al cancelar: un ticket "Resuelto" llega con
    /// isClosed = false. El listado se filtra por estado para no mostrar como
    /// abierto algo que ya terminó. Los nombres salen del flujo real del
    /// modelo 17, leído de api/v9/model/17/4/states.
    /// </summary>
    [Theory]
    [InlineData("Resuelto")]
    [InlineData("Cerrado")]
    [InlineData("Cancelado")]
    public async Task ListOpenTicketsAsync_ExcludesFinishedStates(
        string stateName)
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(
                CreateTicket("collaborator") with
                {
                    IsClosed = false,
                    StateName = stateName
                })
        };
        var service = CreateService(client);

        var result = await service.ListOpenTicketsAsync(
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Empty(result.Value!);
    }

    /// <summary>
    /// Los estados en pausa del modelo 17 (En Aprobacion, Pendiente usuario y
    /// Pendiente proveedor) siguen siendo casos vivos: el colaborador debe
    /// verlos en su listado.
    /// </summary>
    [Theory]
    [InlineData("Registrado")]
    [InlineData("Asignado")]
    [InlineData("En Aprobacion")]
    [InlineData("Pendiente usuario")]
    [InlineData("Pendiente proveedor")]
    [InlineData("En Proceso")]
    public async Task ListOpenTicketsAsync_KeepsUnfinishedStates(
        string stateName)
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(
                CreateTicket("collaborator") with
                {
                    IsClosed = false,
                    StateName = stateName
                })
        };
        var service = CreateService(client);

        var result = await service.ListOpenTicketsAsync(
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task ListOpenTicketsAsync_KeepsTicketsStillInProgress()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(
                CreateTicket("collaborator") with
                {
                    IsClosed = false,
                    StateName = "Registrado"
                })
        };
        var service = CreateService(client);

        var result = await service.ListOpenTicketsAsync(
            CancellationToken.None);

        var ticket = Assert.Single(result.Value!);
        Assert.Equal("Registrado", ticket.Status);
    }

    [Fact]
    public async Task CancelTicketAsync_DoesNotUpdateWithoutConfirmation()
    {
        var client = new StubArandaClient();
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            "CASE-154",
            new("Reason", false),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Null(client.LastUpdateRequest);
    }

    /// <summary>
    /// Estados no anulables segun el DEF, con los nombres reales del modelo 17.
    /// </summary>
    [Theory]
    [InlineData("Resuelto")]
    [InlineData("Cerrado")]
    [InlineData("En Aprobacion")]
    [InlineData("Pendiente usuario")]
    [InlineData("Pendiente proveedor")]
    public async Task CancelTicketAsync_RejectsNonCancellableState(
        string stateName)
    {
        var ticket = CreateTicket("collaborator") with
        {
            StateName = stateName
        };
        var client = new StubArandaClient
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(ticket),
            Ticket = ticket
        };
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            "CASE-154",
            new("Reason", true),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidState,
            result.Status);
        Assert.Null(client.LastUpdateRequest);
    }

    [Fact]
    public async Task CancelTicketAsync_UsesCurrentVersionAndReason()
    {
        var client = CreateClientWithOwnedTicket(
            updateResult: new()
            {
                ItemVersion = 2,
                Result = true
            });
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            "CASE-154",
            new("  User reason  ", true),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal(154, client.LastTicketId);
        Assert.Equal(91, client.LastUpdateRequest?.StateId);
        Assert.Equal(1, client.LastUpdateRequest?.ItemVersion);
        Assert.Equal(8, client.LastUpdateRequest?.RegistryTypeId);
        Assert.Equal(0, client.LastUpdateRequest?.UnitId);
        Assert.Equal(
            "User reason",
            client.LastUpdateRequest?.Commentary);
    }

    [Fact]
    public async Task UploadAttachmentAsync_RejectsUnsupportedExtension()
    {
        var service = CreateService(new StubArandaClient());
        await using var content = new MemoryStream([1]);

        var result = await service.UploadAttachmentAsync(
            "CASE-154",
            new(
                "script.exe",
                "application/octet-stream",
                content.Length,
                content,
                null),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
    }

    [Fact]
    public async Task UploadAttachmentAsync_UploadsToOwnedTicket()
    {
        var client = CreateClientWithOwnedTicket(
            uploadResult:
            [
                new()
                {
                    FileName = "evidence.pdf",
                    Result = true
                }
            ]);
        var service = CreateService(client);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await service.UploadAttachmentAsync(
            "CASE-154",
            new(
                "evidence.pdf",
                "application/pdf",
                content.Length,
                content,
                "Evidence"),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal("evidence.pdf", result.Value?.FileName);
        Assert.Equal(154, client.LastUploadRequest?.TicketId);
    }

    // PARCHE TEMPORAL: mientras la API de usuarios de Aranda no responda, los
    // tickets se operan con un usuario fijo. Al retirar el parche estos tests
    // se eliminan junto con la sección Aranda:UserOverride.
    [Fact]
    public async Task GetTicketDetailAsync_WhenUserOverrideEnabled_UsesFixedUserWithoutCallingUsersApi()
    {
        var ticket = CreateTicket("uebit20@minsur.com");
        var client = new StubArandaClient
        {
            // Sin User: si el servicio llamara a la API de usuarios,
            // el stub lanzaría y la prueba fallaría.
            SearchResult = SearchResultWith(ticket),
            Ticket = ticket
        };
        var service = CreateService(
            client,
            username: "otro.colaborador@minsur.com",
            options: CreateOptions(CreateUserOverride()));

        var result = await service.GetTicketDetailAsync(
            "CASE-154",
            CancellationToken.None);

        Assert.Equal(TicketDetailResultStatus.Success, result.Status);
        Assert.Equal(
            15019L,
            client.LastSearchRequest?.Criteria[0].Value);
    }

    [Fact]
    public async Task CreateTicketAsync_WhenUserOverrideEnabled_RegistersTicketForFixedUser()
    {
        var client = new StubArandaClient
        {
            CreatedTicket = new()
            {
                Id = 154,
                IdByProject = "CASE-154"
            }
        };
        var service = CreateService(
            client,
            username: "otro.colaborador@minsur.com",
            options: CreateOptions(CreateUserOverride()));

        var result = await service.CreateTicketAsync(
            new(TicketKind.ServiceRequest, "Asunto", "Descripción"),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal(15019, client.LastCreateRequest?.CustomerId);
        Assert.Equal(15019, client.LastCreateRequest?.ApplicantId);
    }

    private static ArandaUserOverrideOptions CreateUserOverride() =>
        new()
        {
            Enabled = true,
            Equipos = new()
            {
                Id = 1562,
                UserName = "evelyn.nunez@minsur.com",
                Name = "Evelyn del Carmen Nuñez Girao"
            },
            Tickets = new()
            {
                Id = 15019,
                UserName = "uebit20@minsur.com",
                Name = "UE BIT 20"
            }
        };

    private static TicketService CreateService(
        StubArandaClient client,
        string? username = "collaborator",
        ArandaOptions? options = null) =>
        new(
            new StubCurrentCollaborator(username),
            client,
            Options.Create(options ?? CreateOptions()));

    private static ArandaOptions CreateOptions(
        ArandaUserOverrideOptions? userOverride = null,
        string? subjectPrefix = null,
        int maxDescriptionLength = 20_000,
        ArandaClassificationOptions? classification = null,
        long maxAttachmentBytes = 3_145_728) =>
        new()
        {
            UserOverride = userOverride,
            SubjectPrefix = subjectPrefix,
            MaxDescriptionLength = maxDescriptionLength,
            MaxAttachmentBytes = maxAttachmentBytes,
            Classification = classification ?? new(),
            BaseUrl = new("https://aranda.example/"),
            ApiKey = "Bearer test",
            ProjectId = 1,
            AuthorId = 2,
            CategoryId = 3,
            ServiceId = 4,
            ImpactId = 5,
            UrgencyId = 6,
            GroupId = 7,
            RegistryTypeId = 8,
            UnitId = 9,
            IncidentModelId = 9,
            IncidentInitialStateId = 10,
            IncidentCancellationStateId = 11,
            ServiceRequestModelId = 12,
            ServiceRequestInitialStateId = 13,
            ServiceRequestCancellationStateId = 91
        };

    private static StubArandaClient CreateClientReadyToCreate() =>
        new()
        {
            User = CreateUser(),
            CreatedTicket = new()
            {
                Id = 200,
                IdByProject = "RF-200"
            }
        };

    private static StubArandaClient CreateClientWithOwnedTicket(
        ArandaUpdateTicketResult? updateResult = null,
        IReadOnlyList<ArandaFileUploadResult>? uploadResult = null)
    {
        var ticket = CreateTicket("collaborator");
        return new()
        {
            User = CreateUser(),
            SearchResult = SearchResultWith(ticket),
            Ticket = ticket,
            UpdateResult = updateResult,
            UploadResult = uploadResult
        };
    }

    private static ArandaPagedResponse<ArandaTicket> SearchResultWith(
        params ArandaTicket[] tickets) =>
        new()
        {
            Content = tickets,
            TotalItems = tickets.Length,
            TotalPage = 1
        };

    private static ArandaPagedResponse<ArandaTicket> EmptySearchResult() =>
        SearchResultWith();

    private static ArandaUser CreateUser() =>
        new()
        {
            Id = 10,
            UserName = "collaborator",
            Name = "Collaborator",
            IsActive = true
        };

    private static ArandaTicket CreateTicket(string customerUserName) =>
        new()
        {
            Id = 154,
            IdByProject = "CASE-154",
            CustomerId = 10,
            CustomerUserName = customerUserName,
            Subject = "Subject",
            StateId = 1,
            StateName = "En proceso",
            OpenedDate = 1,
            ModifiedDate = 2,
            GroupName = "Support",
            IsClosed = false,
            ItemVersion = 1,
            ModelId = 12,
            ProjectId = 1,
            RegistryTypeId = 8,
            ServiceId = 4,
            CategoryId = 3,
            ItemType = 4
        };

    private sealed record StubCurrentCollaborator(
        string? Username) : ICurrentCollaborator;

    private sealed class StubArandaClient : IArandaClient
    {
        public ArandaUser? User { get; init; }

        public ArandaTicket? Ticket { get; init; }

        public ArandaPagedResponse<ArandaTicket>? SearchResult
        {
            get;
            init;
        }

        public ArandaCreatedTicket? CreatedTicket { get; init; }

        public ArandaUpdateTicketResult? UpdateResult { get; init; }

        public IReadOnlyList<ArandaFileUploadResult>? UploadResult
        {
            get;
            init;
        }

        public ArandaSearchTicketsRequest? LastSearchRequest
        {
            get;
            private set;
        }

        public ArandaCreateTicketRequest? LastCreateRequest
        {
            get;
            private set;
        }

        public ArandaUpdateTicketRequest? LastUpdateRequest
        {
            get;
            private set;
        }

        public ArandaAttachmentUpload? LastUploadRequest
        {
            get;
            private set;
        }

        public long? LastTicketId { get; private set; }

        public Task<ArandaUser> GetUserByUsernameAsync(
            string username,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                User ?? throw new InvalidOperationException());

        public Task<ArandaTicket> GetTicketAsync(
            long ticketId,
            CancellationToken cancellationToken)
        {
            LastTicketId = ticketId;
            return Task.FromResult(
                Ticket ?? throw new InvalidOperationException());
        }

        public Task<ArandaPagedResponse<ArandaTicket>>
            SearchTicketsAsync(
                ArandaSearchTicketsRequest request,
                CancellationToken cancellationToken)
        {
            LastSearchRequest = request;
            return Task.FromResult(
                SearchResult ?? throw new InvalidOperationException());
        }

        public Task<ArandaCreatedTicket> CreateTicketAsync(
            ArandaCreateTicketRequest request,
            CancellationToken cancellationToken)
        {
            LastCreateRequest = request;
            return Task.FromResult(
                CreatedTicket ??
                throw new InvalidOperationException());
        }

        public Task<ArandaUpdateTicketResult> UpdateTicketAsync(
            long ticketId,
            ArandaUpdateTicketRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateRequest = request;
            return Task.FromResult(
                UpdateResult ?? throw new InvalidOperationException());
        }

        public Task<IReadOnlyList<ArandaFileUploadResult>>
            UploadAttachmentAsync(
                ArandaAttachmentUpload request,
                CancellationToken cancellationToken)
        {
            LastUploadRequest = request;
            return Task.FromResult(
                UploadResult ??
                throw new InvalidOperationException());
        }

        public Task<ArandaPagedResponse<ArandaCiItem>> GetCisByUserAndProjectsAsync(
            ArandaCiRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
