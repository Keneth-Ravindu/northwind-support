using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Triage;

public sealed class SlaCalculator(SlaOptions options)
{
    public TimeSpan CalculateWindow(
        TicketPriority priority,
        CustomerTier customerTier)
    {
        var key = priority.ToString();

        if (!options.BaseWindowHours.TryGetValue(key, out var hours))
        {
            throw new InvalidOperationException(
                $"SLA window is not configured for priority {key}.");
        }

        if (customerTier == CustomerTier.Premium)
        {
            hours *= options.PremiumCustomerMultiplier;
        }

        hours = Math.Max(hours, options.MinimumWindowHours);

        return TimeSpan.FromHours(hours);
    }

    public DateTime CalculateDueAtUtc(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime startUtc)
    {
        return startUtc.Add(CalculateWindow(priority, customerTier));
    }
}
