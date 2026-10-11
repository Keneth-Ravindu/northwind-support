using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CustomerRepository(SupportDbContext db)
    : ICustomerRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Set<Customer>()
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == id, ct);

    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Customer>()
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == id, ct);
}
