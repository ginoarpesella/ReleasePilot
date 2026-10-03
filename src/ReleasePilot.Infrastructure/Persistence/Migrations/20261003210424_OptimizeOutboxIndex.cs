using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReleasePilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeOutboxIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_IsProcessed",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAt",
                table: "OutboxMessages",
                column: "CreatedAt",
                filter: "\"IsProcessed\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IsProcessed",
                table: "OutboxMessages",
                column: "IsProcessed");
        }
    }
}
