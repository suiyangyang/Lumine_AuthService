using System;
using Lumine.AuthServer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lumine.AuthServer.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260805120000_PersistPlainTextApiKeys")]
public partial class PersistPlainTextApiKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PlainTextKey",
            table: "ApiKeys",
            type: "varchar(128)",
            maxLength: 128,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PlainTextKey",
            table: "ApiKeys");
    }
}
