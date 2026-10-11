using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket
/// goes through one of its methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, category, and assigned agent are other aggregates
/// and are referenced by ID only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // Required by EF Core.
    }

    /// <summary>
    /// Human-friendly identifier shown to customers, e.g. TCK-0042.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>
    /// Null while the ticket is waiting for an owner.
    /// </summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// When the team has committed to responding by.
    /// Null when no SLA applies.
    /// </summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// True while the ticket still needs work from an agent.
    /// </summary>
    public bool IsOpen =>
        Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer.
    /// The ticket starts with New status, no owner, and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle,
    /// keeping the resolution timestamp in sync.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is closed and cannot be reopened.
    /// </exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed &&
            status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Assigns the ticket to an agent or removes its assignment
    /// when agentId is null.
    ///
    /// Agent eligibility and capacity are checked by the caller.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Sets or updates the ticket's SLA deadline.
    /// Used during automatic triage and escalation.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">
    /// The calculated deadline is not in the future.
    /// </exception>
    public void SetDueDate(DateTime dueAtUtc, DateTime nowUtc)
    {
        if (dueAtUtc <= nowUtc)
        {
            throw new BusinessRuleViolationException(
                "The SLA deadline must be in the future.");
        }

        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Escalates a ticket by exactly one priority level.
    ///
    /// Low      -> Medium
    /// Medium   -> High
    /// High     -> Critical
    ///
    /// Critical tickets cannot be escalated further.
    /// Resolved and Closed tickets cannot be escalated.
    ///
    /// The application layer is responsible for:
    /// - Calculating the new SLA deadline.
    /// - Reassessing agent assignment.
    /// - Recording the escalation history.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is already Critical, Resolved, or Closed.
    /// </exception>
    public void EscalatePriority(DateTime nowUtc)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} cannot be escalated because " +
                $"its status is {Status}.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical " +
                "and cannot be escalated further.");
        }

        Priority = (TicketPriority)((int)Priority + 1);

        UpdatedAtUtc = nowUtc;
    }
}
