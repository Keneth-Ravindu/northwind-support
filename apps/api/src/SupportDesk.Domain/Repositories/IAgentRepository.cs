using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    Task<Agent?> FindBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);

    Task<bool> IsEligibleForEscalationAsync(
        int agentId,
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);
}
