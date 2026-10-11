
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Builds valid tickets using the domain's own behaviour.
/// Tests can customize only the properties relevant to them.
/// </summary>
public sealed class TicketBuilder
{
    private string _reference = "TCK-0001";
    private string _title = "Something is not working";
    private int _customerId = 1;
    private int _categoryId = 1;
    private TicketPriority _priority = TicketPriority.Medium;
    private TicketStatus _status = TicketStatus.New;
    private int? _assignedAgentId;
    private DateTime? _resolvedAtUtc;

    public TicketBuilder WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public TicketBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TicketBuilder WithCustomerId(int customerId)
    {
        _customerId = customerId;
        return this;
    }

    public TicketBuilder WithCategoryId(int categoryId)
    {
        _categoryId = categoryId;
        return this;
    }

    public TicketBuilder WithPriority(TicketPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TicketBuilder WithStatus(TicketStatus status)
    {
        _status = status;
        return this;
    }

    public TicketBuilder AssignedTo(int? agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    public TicketBuilder ResolvedAt(DateTime? resolvedAtUtc)
    {
        _resolvedAtUtc = resolvedAtUtc;
        return this;
    }

    public Ticket Build()
    {
        var ticket = Ticket.Raise(
            _reference,
            _title,
            "A description long enough to be realistic.",
            _customerId,
            _categoryId,
            _priority,
            FixedClock.DefaultNow);

        if (_assignedAgentId is not null)
        {
            ticket.AssignTo(_assignedAgentId, FixedClock.DefaultNow);
        }

        if (_status != TicketStatus.New)
        {
            ticket.ChangeStatus(
                _status,
                _resolvedAtUtc ?? FixedClock.DefaultNow);
        }

        return ticket;
    }
}
