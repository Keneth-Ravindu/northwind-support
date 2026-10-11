using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger,
    SlaCalculator slaCalculator)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(
            request.CustomerId, ct);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var category = await categories.GetByIdAsync(
            request.CategoryId, ct);

        if (category is null)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var now = clock.UtcNow;

        // BR-1: Categories may override the requested priority.
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;

        // BR-2 and BR-3: Calculate the SLA using configuration
        // and the customer's commercial tier.
        var dueAtUtc = slaCalculator.CalculateDueAtUtc(
            priority,
            customer.Tier,
            now);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            customer.Id,
            category.Id,
            priority,
            now);

        ticket.SetDueDate(dueAtUtc, now);

        // BR-4 and BR-5: Find the least-loaded eligible agent.
        var selectedAgent = await agents.FindBestAvailableAsync(
            category.Id,
            category.RequiresSpecialist,
            ct);

        if (selectedAgent is not null)
        {
            ticket.AssignTo(selectedAgent.Id, now);
        }

        await tickets.AddAsync(ticket, ct);
        
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} created with priority {Priority} and SLA deadline {DueAtUtc}.",
            ticket.Reference,
            priority,
            dueAtUtc);

        
        var created = await getTicket.HandleAsync(ticket.Id, ct);

        // Explain how the ticket's effective priority was determined.
        var priorityReason = category.ForcesCriticalPriority
            ? $"Category '{category.Name}' requires Critical priority."
            : request.RequestedPriority is null
                ? "No priority requested; defaulted to Medium."
                : $"Requested priority {priority} was applied.";

        // Explain the customer-specific SLA calculation.
        var slaWindow = slaCalculator.CalculateWindow(
            priority,
            customer.Tier);

        var slaReason =
            $"{customer.Tier} customer: {slaWindow.TotalHours:0.##}-hour " +
            $"SLA window from ticket creation.";

        // Explain the automatic assignment decision.
        var assignmentReason = selectedAgent is null
            ? "No eligible agent was available; ticket remains unassigned."
            : $"Automatically assigned to {selectedAgent.FullName} " +
            "based on eligibility, specialization, capacity, and workload.";

        created.Triage = new TicketTriageDto(
            priorityReason,
            slaReason,
            assignmentReason);

        return created;

    }

}
