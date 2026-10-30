using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumine.AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperDogProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DongleEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VendorId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActivationSourceHardwareKeyId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ManufacturerId = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    PayloadDigest = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BindingState = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BindingRevision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DongleEntitlements", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ManufacturerKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VendorId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ManufacturerId = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ProtectedKey = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyLookupHmac = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturerKeys", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProductionRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OperationId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VendorId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaterialHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitlementId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MaterialJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedBySubject = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRegistrations_DongleEntitlements_EntitlementId",
                        column: x => x.EntitlementId,
                        principalTable: "DongleEntitlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DongleEntitlements_VendorId_ActivationSourceHardwareKeyId",
                table: "DongleEntitlements",
                columns: new[] { "VendorId", "ActivationSourceHardwareKeyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturerKeys_KeyLookupHmac",
                table: "ManufacturerKeys",
                column: "KeyLookupHmac",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturerKeys_VendorId_ManufacturerId",
                table: "ManufacturerKeys",
                columns: new[] { "VendorId", "ManufacturerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRegistrations_EntitlementId",
                table: "ProductionRegistrations",
                column: "EntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRegistrations_VendorId_OperationId",
                table: "ProductionRegistrations",
                columns: new[] { "VendorId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManufacturerKeys");

            migrationBuilder.DropTable(
                name: "ProductionRegistrations");

            migrationBuilder.DropTable(
                name: "DongleEntitlements");

        }
    }
}
