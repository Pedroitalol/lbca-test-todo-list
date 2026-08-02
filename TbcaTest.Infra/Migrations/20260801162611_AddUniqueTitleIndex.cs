using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TbcaTest.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Title_Unique",
                table: "TaskItems",
                column: "Title",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_Title_Unique",
                table: "TaskItems");
        }
    }
}
