using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;

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
        var service = new TicketService(
            new StubCurrentCollaborator("collaborator"),
            client);

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
        var service = new TicketService(
            new StubCurrentCollaborator("collaborator"),
            client);

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
        var service = new TicketService(
            new StubCurrentCollaborator(null),
            new StubArandaClient());

        var result = await service.GetTicketDetailAsync(
            154,
            CancellationToken.None);

        Assert.Equal(
            TicketDetailResultStatus.MissingCollaborator,
            result.Status);
    }

    private static ArandaTicket CreateTicket(string customerUserName) =>
        new()
        {
            Id = 154,
            IdByProject = "CASE-154",
            CustomerId = 10,
            CustomerUserName = customerUserName,
            Subject = "Subject",
            StateId = 1,
            StateName = "Open",
            OpenedDate = 1,
            ModifiedDate = 2,
            GroupName = "Support",
            IsClosed = false,
            ItemVersion = 1,
            ModelId = 1,
            ProjectId = 1,
            ServiceId = 1,
            CategoryId = 1,
            ItemType = 4
        };

    private sealed record StubCurrentCollaborator(
        string? Username) : ICurrentCollaborator;

    private sealed class StubArandaClient : IArandaClient
    {
        public ArandaUser? User { get; init; }

        public ArandaTicket? Ticket { get; init; }

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

        public Task<ArandaPagedResponse<ArandaTicket>> SearchTicketsAsync(
            ArandaSearchTicketsRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ArandaTicket> CreateTicketAsync(
            ArandaCreateTicketRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ArandaTicket> UpdateTicketAsync(
            long ticketId,
            ArandaUpdateTicketRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
