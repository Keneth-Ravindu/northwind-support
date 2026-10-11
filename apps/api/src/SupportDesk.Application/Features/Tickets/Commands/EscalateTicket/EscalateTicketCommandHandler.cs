
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Handles ticket escalation, SLA recalculation,
/// agent reassignment, and escalation history.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaCalculator slaCalculator,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        // 1. Validate the request before changing any state.
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EscalatedBy) ||
            request.EscalatedBy.Trim().Length > 200)
        {
            throw new ArgumentException(
                "EscalatedBy must contain 1–200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 500)
        {
            throw new ArgumentException(
                "Reason must contain 5–500 characters.");
        }

        // 2. Retrieve the ticket.
        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        // 3. Reject tickets that cannot be escalated.
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException(
                "Resolved or closed tickets cannot be escalated.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                "Critical tickets cannot be escalated further.");
        }

        // 4. Retrieve the customer and category.
        var customer = await customers.GetByIdAsync(
            ticket.CustomerId,
            ct)
            ?? throw new NotFoundException(
                "Customer",
                ticket.CustomerId);

        var category = await categories.GetByIdAsync(
            ticket.CategoryId,
            ct)
            ?? throw new NotFoundException(
                "Category",
                ticket.CategoryId);

        var now = clock.UtcNow;

        // 5. Capture the previous state for audit history.
        var previousPriority = ticket.Priority;
        var previousDueAtUtc = ticket.DueAtUtc;
        var previousAgentId = ticket.AssignedAgentId;

        // 6. Escalate exactly one priority level.
        ticket.EscalatePriority(now);

        // 7. Recalculate the SLA from escalation time.
        var newDueAtUtc = slaCalculator.CalculateDueAtUtc(
            ticket.Priority,
            customer.Tier,
            now);

        ticket.SetDueDate(newDueAtUtc, now);

        // 8. Preserve the existing agent if still eligible.
        int? newAgentId = null;

        if (previousAgentId is int currentAgentId &&
            await agents.IsEligibleForEscalationAsync(
                currentAgentId,
                ticket.CategoryId,
                category.RequiresSpecialist,
                ct))
        {
            // Retaining the agent does not consume an
            // additional capacity slot.
            newAgentId = currentAgentId;
        }
        else
        {
            // Find the least-loaded eligible replacement.
            var replacement = await agents.FindBestAvailableAsync(
                ticket.CategoryId,
                category.RequiresSpecialist,
                ct);

            newAgentId = replacement?.Id;
        }

        // 9. Update the assignment only if it changes.
        if (ticket.AssignedAgentId != newAgentId)
        {
            ticket.AssignTo(newAgentId, now);
        }

        // 10. Create an immutable escalation history record.
        var escalation = TicketEscalation.Create(
            ticket.Id,
            previousPriority,
            ticket.Priority,
            previousDueAtUtc,
            ticket.DueAtUtc,
            previousAgentId,
            ticket.AssignedAgentId,
            request.EscalatedBy.Trim(),
            request.Reason.Trim(),
            now);

        await escalations.AddAsync(escalation, ct);

        // 11. Persist the ticket update and history together.
        await unitOfWork.SaveChangesAsync(ct);

        // 12. Return the updated ticket.
        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}
