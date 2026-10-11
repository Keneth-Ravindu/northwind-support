using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// An immutable record of a ticket escalation.
/// Each escalation creates a new record; existing records
/// must never be modified.
/// </summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation()
    {
        // Required by EF Core.
    }

    public int TicketId { get; private set; }

    public TicketPriority PreviousPriority { get; private set; }

    public TicketPriority NewPriority { get; private set; }

    public DateTime? PreviousDueAtUtc { get; private set; }

    public DateTime? NewDueAtUtc { get; private set; }

    public int? PreviousAgentId { get; private set; }

    public int? NewAgentId { get; private set; }

    public string EscalatedBy { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }

    public static TicketEscalation Create(
        int ticketId,
        TicketPriority previousPriority,
        TicketPriority newPriority,
        DateTime? previousDueAtUtc,
        DateTime? newDueAtUtc,
        int? previousAgentId,
        int? newAgentId,
        string escalatedBy,
        string reason,
        DateTime escalatedAtUtc)
    {
        if (ticketId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticketId));

        if (string.IsNullOrWhiteSpace(escalatedBy))
            throw new ArgumentException(
                "The escalation actor is required.",
                nameof(escalatedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "An escalation reason is required.",
                nameof(reason));

        reason = reason.Trim();
        escalatedBy = escalatedBy.Trim();

        if (reason.Length is < 5 or > 500)
            throw new ArgumentException(
                "The escalation reason must contain 5–500 characters.",
                nameof(reason));

        if (escalatedBy.Length > 200)
            throw new ArgumentException(
                "The escalation actor must not exceed 200 characters.",
                nameof(escalatedBy));

        if ((int)newPriority != (int)previousPriority + 1)
            throw new ArgumentException(
                "Escalation must increase priority by exactly one level.",
                nameof(newPriority));

        return new TicketEscalation
        {
            TicketId = ticketId,
            PreviousPriority = previousPriority,
            NewPriority = newPriority,
            PreviousDueAtUtc = previousDueAtUtc,
            NewDueAtUtc = newDueAtUtc,
            PreviousAgentId = previousAgentId,
            NewAgentId = newAgentId,
            EscalatedBy = escalatedBy,
            Reason = reason,
            EscalatedAtUtc = escalatedAtUtc
        };
    }
}
