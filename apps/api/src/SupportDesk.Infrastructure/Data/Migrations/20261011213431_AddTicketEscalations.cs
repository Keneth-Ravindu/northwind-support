using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    PreviousPriority = table.Column<int>(type: "int", nullable: false),
                    NewPriority = table.Column<int>(type: "int", nullable: false),
                    PreviousDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    NewDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PreviousAgentId = table.Column<int>(type: "int", nullable: true),
                    NewAgentId = table.Column<int>(type: "int", nullable: true),
                    EscalatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_EscalatedBy",
                table: "TicketEscalations",
                column: "EscalatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_TicketId_EscalatedAtUtc",
                table: "TicketEscalations",
                columns: new[] { "TicketId", "EscalatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEscalations");
        }
    }
}
