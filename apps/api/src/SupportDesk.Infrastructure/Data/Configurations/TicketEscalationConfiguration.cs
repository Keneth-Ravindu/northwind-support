
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class TicketEscalationConfiguration
    : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PreviousPriority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.NewPriority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.PreviousDueAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.NewDueAtUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.EscalatedBy)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.EscalatedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new
            {
                e.TicketId,
                e.EscalatedAtUtc
            })
            .HasDatabaseName(
                "IX_TicketEscalations_TicketId_EscalatedAtUtc");

        builder.HasIndex(e => e.EscalatedBy)
            .HasDatabaseName(
                "IX_TicketEscalations_EscalatedBy");
    }
}
