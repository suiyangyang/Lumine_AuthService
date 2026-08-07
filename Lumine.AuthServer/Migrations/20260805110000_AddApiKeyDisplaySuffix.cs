using System;
using Lumine.AuthServer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumine.AuthServer.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260805110000_AddApiKeyDisplaySuffix")]
public partial class AddApiKeyDisplaySuffix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "KeySuffix",
            table: "ApiKeys",
            type: "varchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "???")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "KeySuffix",
            table: "ApiKeys");
    }
}
