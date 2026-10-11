using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared test context for ticket command handlers.
/// Provides mocked repositories, a fixed clock, configurable SLA rules,
/// and handlers wired to their dependencies.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        // Customer repository mocks
       Customers
            .Setup(c => c.ExistsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Customer(
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium,
                    Clock.UtcNow));

        // Category repository mocks
        Categories
            .Setup(c => c.ExistsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Category(
                    "General",
                    requiresSpecialist: false,
                    forcesCriticalPriority: false));

        // Ticket repository mocks
        Tickets
            .Setup(t => t.NextReferenceAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets
            .Setup(t => t.AddAsync(
                It.IsAny<Ticket>(),
                It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>(
                (ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Ticket query mock

        // Existing command handlers return the ticket after
        // reading its updated details.
        TicketQueries
            .Setup(q => q.GetDetailAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",

                Customer = new CustomerContactDto(
                    1,
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium),

                Category = new CategoryDto(
                    1,
                    "General",
                    false)
            });

        var getTicket = new GetTicketQueryHandler(
            TicketQueries.Object);
        // SLA configuration for unit tests

        // These values match the development configuration.
        // Tests remain independent of appsettings.json.

        var slaOptions = new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>
            {
                ["Low"] = 72,
                ["Medium"] = 24,
                ["High"] = 8,
                ["Critical"] = 4
            },

            PremiumCustomerMultiplier = 0.5,

            MinimumWindowHours = 1,

            AtRiskThresholdPercent = 25
        };

        var slaCalculator = new SlaCalculator(slaOptions);
        
        // Command handlers
        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance,
            slaCalculator);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(
            Tickets.Object,
            UnitOfWork.Object,
            Clock,
            getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    // Fixed clock
    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    // Captured ticket

    /// <summary>
    /// The ticket passed to the repository during the most recent
    /// ticket creation operation.
    /// </summary>
    public Ticket? AddedTicket { get; private set; }
 
    // Test helper methods

    /// <summary>
    /// Configures the ticket repository to return the specified ticket.
    /// </summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets
            .Setup(t => t.GetByIdAsync(
                ticket.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    /// <summary>
    /// Configures the agent repository to return a test agent.
    /// </summary>
    public void ExistingAgent(
        int id,
        string fullName = "Alex Turner",
        bool isActive = true)
    {
        var agent = new Agent(
            fullName,
            $"agent{id}@example.com",
            maxOpenTickets: 10,
            Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents
            .Setup(a => a.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
    }

    /// <summary>
    /// Verifies that the unit of work saved the expected number
    /// of times.
    /// </summary>
    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(
            u => u.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            times);
}
