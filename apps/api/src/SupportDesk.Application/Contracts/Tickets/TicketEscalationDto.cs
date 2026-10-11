using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Read-only API representation of an escalation history entry.
/// </summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority PreviousPriority,
    TicketPriority NewPriority,
    DateTime? PreviousDueAtUtc,
    DateTime? NewDueAtUtc,
    int? PreviousAgentId,
    int? NewAgentId,
    string EscalatedBy,
    string Reason,
    DateTime EscalatedAtUtc);
