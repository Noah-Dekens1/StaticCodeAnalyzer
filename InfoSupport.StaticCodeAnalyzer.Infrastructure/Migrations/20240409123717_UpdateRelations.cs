using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Migrations
{
    [ExcludeFromCodeCoverage]
    /// <inheritdoc />
    public partial class UpdateRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_ProjectFiles_ProjectFileId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFiles_Reports_ReportId",
                table: "ProjectFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Projects_ProjectId",
                table: "Reports");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_ProjectFiles_ProjectFileId",
                table: "Issues",
                column: "ProjectFileId",
                principalTable: "ProjectFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFiles_Reports_ReportId",
                table: "ProjectFiles",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Projects_ProjectId",
                table: "Reports",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_ProjectFiles_ProjectFileId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFiles_Reports_ReportId",
                table: "ProjectFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Projects_ProjectId",
                table: "Reports");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_ProjectFiles_ProjectFileId",
                table: "Issues",
                column: "ProjectFileId",
                principalTable: "ProjectFiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFiles_Reports_ReportId",
                table: "ProjectFiles",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Projects_ProjectId",
                table: "Reports",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}
