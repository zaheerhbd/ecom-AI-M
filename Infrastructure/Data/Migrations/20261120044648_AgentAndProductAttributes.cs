using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgentAndProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "Products",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rating",
                table: "Products",
                type: "numeric(2,1)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "StockQuantity",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Products that already exist get sensible values instead of 0 (docs/agentic/04 §2).
            // This only runs once, when the migration is applied; new products come from the seed.
            migrationBuilder.Sql("UPDATE \"Products\" SET \"StockQuantity\" = 25;");
            migrationBuilder.Sql("UPDATE \"Products\" SET \"Rating\" = 4.0;");

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    ToolsUsed = table.Column<string>(type: "jsonb", nullable: false),
                    Steps = table.Column<string>(type: "jsonb", nullable: false),
                    GuardrailChecks = table.Column<string>(type: "jsonb", nullable: false),
                    Sources = table.Column<string>(type: "jsonb", nullable: false),
                    EvalRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvalRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UseJudge = table.Column<bool>(type: "boolean", nullable: false),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalCases = table.Column<int>(type: "integer", nullable: false),
                    CompletedCases = table.Column<int>(type: "integer", nullable: false),
                    PassedCases = table.Column<int>(type: "integer", nullable: false),
                    FlakyCases = table.Column<int>(type: "integer", nullable: false),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false),
                    PassedAttempts = table.Column<int>(type: "integer", nullable: false),
                    PassRate = table.Column<double>(type: "double precision", nullable: false),
                    CorrectToolRate = table.Column<double>(type: "double precision", nullable: true),
                    MedianLatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    P95LatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    MedianTokens = table.Column<double>(type: "double precision", nullable: false),
                    TotalCostUsd = table.Column<decimal>(type: "numeric(10,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvalCaseResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvalRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Flaky = table.Column<bool>(type: "boolean", nullable: false),
                    RepeatCount = table.Column<int>(type: "integer", nullable: false),
                    PassCount = table.Column<int>(type: "integer", nullable: false),
                    Checks = table.Column<string>(type: "jsonb", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    JudgeScore = table.Column<int>(type: "integer", nullable: true),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    Tokens = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalCaseResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvalCaseResults_EvalRuns_EvalRunId",
                        column: x => x.EvalRunId,
                        principalTable: "EvalRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_CreatedAt",
                table: "AgentRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_EvalRunId",
                table: "AgentRuns",
                column: "EvalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalCaseResults_EvalRunId",
                table: "EvalCaseResults",
                column: "EvalRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRuns_StartedAt",
                table: "EvalRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "EvalCaseResults");

            migrationBuilder.DropTable(
                name: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "Products");
        }
    }
}
