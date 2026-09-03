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
                        StateId = 66,
                        StateName = "Resuelto",
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
        Assert.Equal("Resuelto", ticket.Status);
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

    [Fact]
    public async Task CancelTicketAsync_RejectsNonCancellableState()
    {
        var ticket = CreateTicket("collaborator") with
        {
            StateName = "Resuelto"
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
            UnitId = 9,
            IncidentModelId = 9,
            IncidentInitialStateId = 10,
            IncidentCancellationStateId = 11,
            ServiceRequestModelId = 12,
            ServiceRequestInitialStateId = 13,
            ServiceRequestCancellationStateId = 91
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
    }
}
