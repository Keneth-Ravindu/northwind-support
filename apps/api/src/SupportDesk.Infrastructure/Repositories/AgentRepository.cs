using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db)
    : IAgentRepository
{
    /// <summary>
    /// Loads the agent aggregate, including specializations.
    /// </summary>
    public Task<Agent?> GetByIdAsync(
        int id,
        CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Selects the active, qualified agent with the fewest open tickets.
    /// Agents at their maximum capacity are excluded.
    /// Agent ID provides a deterministic tie-break.
    /// </summary>
    public Task<Agent?> FindBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        var agents = db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (requiresSpecialist)
        {
            agents = agents.Where(a =>
                a.Specializations.Any(s =>
                    s.CategoryId == categoryId));
        }

        var eligibleAgents = agents
            .Select(a => new
            {
                Agent = a,

                OpenTicketCount = db.Set<Ticket>()
                    .Count(t =>
                        t.AssignedAgentId == a.Id &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Closed)
            })
            .Where(x =>
                x.OpenTicketCount < x.Agent.MaxOpenTickets)
            .OrderBy(x => x.OpenTicketCount)
            .ThenBy(x => x.Agent.Id);

        return eligibleAgents
            .Select(x => x.Agent)
            .FirstOrDefaultAsync(ct);
    }


    public async Task<bool> IsEligibleForEscalationAsync(
        int agentId,
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.Id == agentId && a.IsActive)
            .Select(a => new
            {
                a.MaxOpenTickets,
                HasSpecialization = a.Specializations.Any(
                    s => s.CategoryId == categoryId),
                OpenTicketCount = db.Set<Ticket>().Count(
                    t => t.AssignedAgentId == a.Id &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Closed)
            })
            .FirstOrDefaultAsync(ct);

        if (agent is null)
            return false;

        if (requiresSpecialist && !agent.HasSpecialization)
            return false;

        // The existing ticket already occupies one capacity slot.
        // Retaining its current owner does not add another ticket.
        return agent.OpenTicketCount <= agent.MaxOpenTickets;
    }

}
