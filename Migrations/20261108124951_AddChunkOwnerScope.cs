using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace chatbotchaytau.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkOwnerScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Chunks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_OwnerId_DocumentId",
                table: "Chunks",
                columns: new[] { "OwnerId", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chunks_OwnerId_DocumentId",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Chunks");
        }
    }
}
