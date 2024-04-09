using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRunAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RunAt",
                table: "Reports",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunAt",
                table: "Reports");
        }
    }
}
