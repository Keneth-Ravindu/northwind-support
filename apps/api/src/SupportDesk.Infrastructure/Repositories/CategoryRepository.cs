using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CategoryRepository(SupportDbContext db)
    : ICategoryRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Set<Category>()
            .AsNoTracking()
            .AnyAsync(category => category.Id == id, ct);

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Category>()
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Id == id, ct);
}
