using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Migrations
{
    [ExcludeFromCodeCoverage]
    /// <inheritdoc />
    public partial class IssueSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Issues");

            migrationBuilder.AddColumn<int>(
                name: "AnalyzerSeverity",
                table: "Issues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalyzerSeverity",
                table: "Issues");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Issues",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
