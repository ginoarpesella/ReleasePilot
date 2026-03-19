using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReleasePilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Promotions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ApplicationName_TargetEnvironment",
                table: "Promotions",
                columns: new[] { "ApplicationName", "TargetEnvironment" },
                unique: true,
                filter: "\"Status\" = 'InProgress'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Promotions_ApplicationName_TargetEnvironment",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Promotions");
        }
    }
}
