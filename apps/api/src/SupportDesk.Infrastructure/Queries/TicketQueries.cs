
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked.
/// Aggregates hold only each other's ids, so customer, category,
/// and agent labels are joined on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock) : ITicketQueries
{
    /// <summary>
    /// Returns a filtered, sorted and paginated list of tickets.
    /// </summary>
    /// <remarks>
    /// All optional filters are applied in the database before counting,
    /// sorting and pagination. Multiple filters combine with AND.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        // Build the database query and apply optional filters.
        // No database records are loaded at this stage.
        var tickets = ApplyFilters(TicketsWithLabels(), query);

        // Count only tickets matching the supplied filters.
        var totalCount = await tickets.CountAsync(ct);

        var now = clock.UtcNow;

        // Sorting and pagination are performed by the database.
        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    /// <summary>
    /// Retrieves a single ticket with its related labels.
    /// </summary>
    public Task<TicketDetailDto?> GetDetailAsync(
        int id,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Email,
                    x.Customer.Phone,
                    x.Customer.Tier),
                Category = new CategoryDto(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Retrieves all tickets belonging to a particular customer.
    /// </summary>
    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Constructs a read-only query that joins tickets with their
    /// customer, category and optionally assigned agent.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>().AsNoTracking()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>().AsNoTracking()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    /// <summary>
    /// Applies optional ticket filters using IQueryable.
    /// All predicates are translated into SQL by Entity Framework Core.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        // FL-1: Exact status matching.
        if (query.Status is { } status)
        {
            source = source.Where(x =>
                x.Ticket.Status == status);
        }

        // FL-1: Exact priority matching.
        if (query.Priority is { } priority)
        {
            source = source.Where(x =>
                x.Ticket.Priority == priority);
        }

        // FL-1: Category matching.
        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x =>
                x.Ticket.CategoryId == categoryId);
        }

        // FL-1: Customer matching.
        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x =>
                x.Ticket.CustomerId == customerId);
        }

        // FL-1: Assigned agent matching.
        if (query.AssignedAgentId is { } assignedAgentId)
        {
            source = source.Where(x =>
                x.Ticket.AssignedAgentId == assignedAgentId);
        }

        // FL-2: Only tickets without an assigned agent.
        if (query.UnassignedOnly)
        {
            source = source.Where(x =>
                x.Ticket.AssignedAgentId == null);
        }

        // FL-3: Search title, reference or customer name.
        // Trim whitespace and ignore blank searches.
        var search = query.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Escape LIKE wildcard characters so user input
            // is treated as literal text.
            var pattern = $"%{EscapeLikePattern(search)}%";

            source = source.Where(x =>
                EF.Functions.Like(
                    x.Ticket.Title,
                    pattern,
                    "\\") ||
                EF.Functions.Like(
                    x.Ticket.Reference,
                    pattern,
                    "\\") ||
                EF.Functions.Like(
                    x.Customer.Name,
                    pattern,
                    "\\"));
        }

        // FL-4: Every Where clause composes with AND.
        // FL-6: Future filters can be added here.
        return source;
    }

    /// <summary>
    /// Escapes LIKE pattern characters for literal substring searches.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

    /// <summary>
    /// Applies the requested sorting with a deterministic tie-break.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),

            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),

            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),

            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),

            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps pagination stable when sorting values are identical.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    /// <summary>
    /// Internal projection used to construct composable database queries.
    /// </summary>
    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
