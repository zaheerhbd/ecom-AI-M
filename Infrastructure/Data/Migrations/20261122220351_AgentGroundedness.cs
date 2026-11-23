using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgentGroundedness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FactsChecked",
                table: "EvalRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MadeUpFacts",
                table: "EvalRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MadeUpRate",
                table: "EvalRuns",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroundedFactCount",
                table: "AgentRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MadeUpFacts",
                table: "AgentRuns",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FactsChecked",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "MadeUpFacts",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "MadeUpRate",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "GroundedFactCount",
                table: "AgentRuns");

            migrationBuilder.DropColumn(
                name: "MadeUpFacts",
                table: "AgentRuns");
        }
    }
}
