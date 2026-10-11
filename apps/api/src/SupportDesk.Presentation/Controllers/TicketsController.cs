using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(int id, [FromServices] GetTicketQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it when no agent is given.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);


    /// <summary>
    /// Escalates a ticket by one priority level,
    /// recalculates the SLA, and reassesses its assignment.
    /// </summary>
    [HttpPost("{id:int}/escalate")]
    public async Task<ActionResult<TicketDetailDto>> Escalate(
        int id,
        [FromBody] EscalateTicketRequest request,
        [FromServices] EscalateTicketCommandHandler handler,
        CancellationToken ct)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.EscalatedBy) ||
            request.EscalatedBy.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 500)
        {
            return BadRequest(new
            {
                message = "EscalatedBy must be 1–200 characters and Reason must be 5–500 characters."
            });
        }

        var updated = await handler.HandleAsync(id, request, ct);

        return Ok(updated);
    }

    /// <summary>
    /// Returns a ticket's escalation history, newest first.
    /// </summary>
    [HttpGet("{id:int}/escalations")]
    public async Task<ActionResult<IReadOnlyList<TicketEscalationDto>>> GetEscalations(
        int id,
        [FromServices] ITicketRepository tickets,
        [FromServices] ITicketEscalationRepository escalations,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);

        if (ticket is null)
        {
            return NotFound(new { message = $"Ticket {id} was not found." });
        }

        var history = await escalations.GetByTicketIdAsync(id, ct);

        var result = history
            .Select(e => new TicketEscalationDto(
                e.Id,
                e.TicketId,
                e.PreviousPriority,
                e.NewPriority,
                e.PreviousDueAtUtc,
                e.NewDueAtUtc,
                e.PreviousAgentId,
                e.NewAgentId,
                e.EscalatedBy,
                e.Reason,
                e.EscalatedAtUtc))
            .ToList();

        return Ok(result);
    }

}
