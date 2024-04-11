using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoSupport.StaticCodeAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilesInDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "ProjectFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "ProjectFiles");
        }
    }
}
