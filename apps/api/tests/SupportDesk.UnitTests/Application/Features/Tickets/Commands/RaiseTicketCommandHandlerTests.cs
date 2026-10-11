using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(
        TicketPriority? priority = null) =>
        new(
            "Cannot log in to the portal",
            "Happens for every user since this morning.",
            1,
            3,
            priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);

        // Premium customer: High SLA = 8 hours * 0.5 = 4 hours.
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(4),
            created.DueAtUtc);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);
        Assert.Equal(TicketPriority.Medium, created.Priority);

        // Premium customer: Medium SLA = 24 hours * 0.5 = 12 hours.
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(12),
            created.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        // The handler now retrieves the customer instead of
        // calling ExistsAsync().
        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportDesk.Domain.Aggregates.Customers.Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Customer", exception.Message);

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportDesk.Domain.Aggregates.Categories.Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Category", exception.Message);

        context.VerifySaved(Times.Never());
    }


    [Fact]
    public async Task HandleAsync_AssignsSelectedEligibleAgent()
    {
        var context = new TicketCommandTestContext();

        var agent = new SupportDesk.Domain.Aggregates.Agents.Agent(
            "Alex Turner",
            "alex@example.com",
            maxOpenTickets: 10,
            context.Clock.UtcNow);

        context.Agents
            .Setup(a => a.FindBestAvailableAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        // The handler should assign the agent returned by the repository.
        Assert.Equal(agent.Id, context.AddedTicket.AssignedAgentId);

        context.Agents.Verify(
            a => a.FindBestAvailableAsync(
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

}
