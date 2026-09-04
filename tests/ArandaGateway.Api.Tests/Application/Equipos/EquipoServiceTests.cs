using System.Net;
using ArandaGateway.Api.Application.Equipos;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Tests.Application.Equipos;

public sealed class EquipoServiceTests
{
    [Fact]
    public async Task ListAssignedEquiposAsync_RequiresCollaborator()
    {
        var service = CreateService(
            new StubArandaClient(),
            username: null);

        var result = await service.ListAssignedEquiposAsync(CancellationToken.None);

        Assert.Equal(EquipoOperationResultStatus.MissingCollaborator, result.Status);
    }

    [Fact]
    public async Task ListAssignedEquiposAsync_WhenNoCisReturned_ReturnsNoRecordsFound()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            CisResponse = new()
            {
                Content = [],
                TotalItems = 0,
                TotalPage = 0
            }
        };
        var service = CreateService(client);

        var result = await service.ListAssignedEquiposAsync(CancellationToken.None);

        Assert.Equal(EquipoOperationResultStatus.NoRecordsFound, result.Status);
    }

    [Fact]
    public async Task ListAssignedEquiposAsync_WhenCisExist_MapsOnlyAuthorizedFields()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            CisResponse = new()
            {
                Content =
                [
                    new()
                    {
                        Id = 101,
                        CiTypeName = "Laptop",
                        ModelName = "ThinkPad T14",
                        Code = "ACT-00982",
                        StateName = "Asignado"
                    }
                ],
                TotalItems = 1,
                TotalPage = 1
            }
        };
        var service = CreateService(client);

        var result = await service.ListAssignedEquiposAsync(CancellationToken.None);

        Assert.Equal(EquipoOperationResultStatus.Success, result.Status);
        var equipo = Assert.Single(result.Value!);
        Assert.Equal("Laptop", equipo.Tipo);
        Assert.Equal("ThinkPad T14", equipo.Modelo);
        Assert.Equal("ACT-00982", equipo.Codigo);
        Assert.Equal("Asignado", equipo.Estado);
        Assert.Equal(10, client.LastCiRequest?.UserId); 
        Assert.Equal(1, client.LastCiRequest?.Projects[0].Project);
    }

    [Fact]
    public async Task ListAssignedEquiposAsync_WhenSourceFails_ReturnsSourceUnavailable()
    {
        var client = new StubArandaClient
        {
            User = CreateUser(),
            ThrowOnCis = true
        };
        var service = CreateService(client);

        var result = await service.ListAssignedEquiposAsync(CancellationToken.None);

        Assert.Equal(EquipoOperationResultStatus.SourceUnavailable, result.Status);
    }

    private static EquipoService CreateService(
        StubArandaClient client,
        string? username = "collaborator",
        ArandaOptions? options = null) =>
        new(
            new StubCurrentCollaborator(username),
            client,
            Options.Create(options ?? CreateOptions()),
            NullLogger<EquipoService>.Instance);

    private static ArandaOptions CreateOptions() =>
        new()
        {
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

    private static ArandaUser CreateUser(string username = "collaborator") =>
        new()
        {
            Id = 10,
            UserName = username,
            Name = "Collaborator",
            IsActive = true
        };

    private sealed record StubCurrentCollaborator(
        string? Username) : ICurrentCollaborator;

    private sealed class StubArandaClient : IArandaClient
    {
        public ArandaUser? User { get; init; }
        public ArandaPagedResponse<ArandaCiItem>? CisResponse { get; init; }
        public bool ThrowOnCis { get; init; }
        public ArandaCiRequest? LastCiRequest { get; private set; }

        public Task<ArandaUser> GetUserByUsernameAsync(
            string username,
            CancellationToken cancellationToken) =>
            Task.FromResult(User ?? throw new ArandaApiException(HttpStatusCode.NotFound));

        public Task<ArandaPagedResponse<ArandaCiItem>> GetCisByUserAndProjectsAsync(
            ArandaCiRequest request,
            CancellationToken cancellationToken)
        {
            LastCiRequest = request;
            if (ThrowOnCis)
            {
                throw new ArandaApiException(HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult(CisResponse ?? throw new InvalidOperationException());
        }

        public Task<ArandaTicket> GetTicketAsync(long ticketId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ArandaPagedResponse<ArandaTicket>> SearchTicketsAsync(
            ArandaSearchTicketsRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ArandaCreatedTicket> CreateTicketAsync(
            ArandaCreateTicketRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ArandaUpdateTicketResult> UpdateTicketAsync(
            long ticketId, ArandaUpdateTicketRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ArandaFileUploadResult>> UploadAttachmentAsync(
            ArandaAttachmentUpload request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}