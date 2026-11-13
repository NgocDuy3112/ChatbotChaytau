using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace chatbotchaytau.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: true),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: true),
                    CostUsd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ToolCalls = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_Model",
                table: "UsageEvents",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_Timestamp",
                table: "UsageEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_UserId_Timestamp",
                table: "UsageEvents",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageEvents");
        }
    }
}
