namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Request body for POST /api/tickets/{id}/escalate.
/// </summary>
public sealed record EscalateTicketRequest(
    string EscalatedBy,
    string Reason);
