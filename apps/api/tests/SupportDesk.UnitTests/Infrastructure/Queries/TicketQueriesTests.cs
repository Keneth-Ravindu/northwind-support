
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    private static async Task<TestContext> CreateContextAsync()
    {
        // Keep the SQLite in-memory database alive for the test.
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        // Create real related entities.
        
        var contoso = new Customer(
            "Contoso Ltd",
            "support@contoso.test",
            null,
            CustomerTier.Standard,
            now);

        var fabrikam = new Customer(
            "Fabrikam Inc",
            "support@fabrikam.test",
            null,
            CustomerTier.Premium,
            now);


        var billing = new Category("Billing", false, false);
        var technical = new Category("Technical", false, false);

        var agent = new Agent(
            "Alex Morgan",
            "alex@example.test",
            10,
            now);

        db.Set<Customer>().AddRange(contoso, fabrikam);
        db.Set<Category>().AddRange(billing, technical);
        db.Set<Agent>().Add(agent);

        await db.SaveChangesAsync();

        // Four tickets with deliberately different properties.
        var tickets = new[]
        {
            new TicketBuilder()
                .WithReference("TCK-0001")
                .WithTitle("Cannot sign in")
                .WithCustomerId(contoso.Id)
                .WithCategoryId(technical.Id)
                .WithPriority(TicketPriority.High)
                .WithStatus(TicketStatus.InProgress)
                .AssignedTo(agent.Id)
                .Build(),

            new TicketBuilder()
                .WithReference("TCK-0002")
                .WithTitle("Invoice correction")
                .WithCustomerId(contoso.Id)
                .WithCategoryId(billing.Id)
                .WithPriority(TicketPriority.High)
                .Build(),

            new TicketBuilder()
                .WithReference("TCK-0003")
                .WithTitle("Application crashes")
                .WithCustomerId(fabrikam.Id)
                .WithCategoryId(technical.Id)
                .WithPriority(TicketPriority.Low)
                .Build(),

            new TicketBuilder()
                .WithReference("TCK-0004")
                .WithTitle("Billing question")
                .WithCustomerId(fabrikam.Id)
                .WithCategoryId(billing.Id)
                .WithPriority(TicketPriority.Medium)
                .AssignedTo(agent.Id)
                .Build()
        };

        db.Set<Ticket>().AddRange(tickets);
        await db.SaveChangesAsync();

        return new TestContext(
            connection,
            db,
            new TicketQueries(db, new FixedClock()),
            contoso.Id,
            billing.Id,
            agent.Id);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriority()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
            Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_CombinesFiltersUsingAnd()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);

        Assert.Equal("TCK-0001", item.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery { Search = "  Contoso  " },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(result.Items, item =>
            Assert.Equal("Contoso Ltd", item.Customer.Name));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersUnassignedTickets()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);

        Assert.All(result.Items, item =>
            Assert.Null(item.AssignedAgent));
    }

    [Fact]
    public async Task GetPagedAsync_AppliesFilteringBeforePagination()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByCustomerAndCategory()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery
            {
                CustomerId = context.CustomerId,
                CategoryId = context.CategoryId
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);

        Assert.Equal("TCK-0002", item.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByAssignedAgent()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery
            {
                AssignedAgentId = context.AgentId
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
            Assert.NotNull(item.AssignedAgent));
    }

    [Fact]
    public async Task GetPagedAsync_IgnoresWhitespaceOnlySearch()
    {
        await using var context = await CreateContextAsync();

        var result = await context.Queries.GetPagedAsync(
            new TicketQuery { Search = "   " },
            CancellationToken.None);

        Assert.Equal(4, result.TotalCount);
    }

    private sealed class TestContext(
        SqliteConnection connection,
        SupportDbContext db,
        TicketQueries queries,
        int customerId,
        int categoryId,
        int agentId) : IAsyncDisposable
    {
        public TicketQueries Queries { get; } = queries;
        public int CustomerId { get; } = customerId;
        public int CategoryId { get; } = categoryId;
        public int AgentId { get; } = agentId;

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
