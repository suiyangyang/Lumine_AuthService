using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumine.AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class TrackSuperDogProductionRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionRegistrations_EntitlementId",
                table: "ProductionRegistrations");

            migrationBuilder.AddColumn<int>(
                name: "ProductionRevision",
                table: "ProductionRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionRevision",
                table: "DongleEntitlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRegistrations_EntitlementId_ProductionRevision",
                table: "ProductionRegistrations",
                columns: new[] { "EntitlementId", "ProductionRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionRegistrations_EntitlementId_ProductionRevision",
                table: "ProductionRegistrations");

            migrationBuilder.DropColumn(
                name: "ProductionRevision",
                table: "ProductionRegistrations");

            migrationBuilder.DropColumn(
                name: "ProductionRevision",
                table: "DongleEntitlements");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRegistrations_EntitlementId",
                table: "ProductionRegistrations",
                column: "EntitlementId");
        }
    }
}
