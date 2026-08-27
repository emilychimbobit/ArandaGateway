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
        var client = new StubArandaClient
        {
            Ticket = CreateTicket("collaborator")
        };
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            154,
            CancellationToken.None);

        Assert.Equal(TicketDetailResultStatus.Success, result.Status);
        Assert.Equal("CASE-154", result.Ticket?.CaseNumber);
        Assert.Null(result.Ticket?.Solution);
    }

    [Fact]
    public async Task GetTicketDetailAsync_HidesTicketOwnedByAnotherUser()
    {
        var client = new StubArandaClient
        {
            Ticket = CreateTicket("another-user")
        };
        var service = CreateService(client);

        var result = await service.GetTicketDetailAsync(
            154,
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
            154,
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.MissingCollaborator,
            result.Status);
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
        Assert.Equal("Subject", client.LastCreateRequest?.Subject);
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
        Assert.Equal(1, client.LastSearchRequest?.Repository);
        Assert.Equal("customerId",
            client.LastSearchRequest?.Criteria.Single().FieldName);
    }

    [Fact]
    public async Task CancelTicketAsync_DoesNotUpdateWithoutConfirmation()
    {
        var client = new StubArandaClient();
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            154,
            new("Reason", false),
            CancellationToken.None);

        Assert.Equal(
            TicketOperationResultStatus.InvalidRequest,
            result.Status);
        Assert.Null(client.LastUpdateRequest);
    }

    [Fact]
    public async Task CancelTicketAsync_RejectsNonCancellableState()
    {
        var client = new StubArandaClient
        {
            Ticket = CreateTicket("collaborator") with
            {
                StateName = "Resuelto"
            }
        };
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            154,
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
        var client = new StubArandaClient
        {
            Ticket = CreateTicket("collaborator"),
            UpdateResult = new()
            {
                ItemVersion = 2,
                Result = true
            }
        };
        var service = CreateService(client);

        var result = await service.CancelTicketAsync(
            154,
            new("  User reason  ", true),
            CancellationToken.None);

        Assert.Equal(TicketOperationResultStatus.Success, result.Status);
        Assert.Equal(91, client.LastUpdateRequest?.StateId);
        Assert.Equal(1, client.LastUpdateRequest?.ItemVersion);
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
            154,
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
        var client = new StubArandaClient
        {
            Ticket = CreateTicket("collaborator"),
            UploadResult =
            [
                new()
                {
                    FileName = "evidence.pdf",
                    Result = true
                }
            ]
        };
        var service = CreateService(client);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await service.UploadAttachmentAsync(
            154,
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

    private static TicketService CreateService(
        StubArandaClient client,
        string? username = "collaborator",
        ArandaOptions? options = null) =>
        new(
            new StubCurrentCollaborator(username),
            client,
            Options.Create(options ?? CreateOptions()));

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
            IncidentModelId = 9,
            IncidentInitialStateId = 10,
            IncidentCancellationStateId = 11,
            ServiceRequestModelId = 12,
            ServiceRequestInitialStateId = 13,
            ServiceRequestCancellationStateId = 91
        };

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

        public Task<ArandaUser> GetUserByUsernameAsync(
            string username,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                User ?? throw new InvalidOperationException());

        public Task<ArandaTicket> GetTicketAsync(
            long ticketId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Ticket ?? throw new InvalidOperationException());

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
    }
}
