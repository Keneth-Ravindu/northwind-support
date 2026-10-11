namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains the decisions made during automatic ticket triage.
/// </summary>
public sealed record TicketTriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
