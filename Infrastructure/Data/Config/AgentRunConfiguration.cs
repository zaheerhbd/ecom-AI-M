using Agent.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
    /// <summary>Maps the agent's run record to the AgentRuns table (see docs/agentic/04 §3).</summary>
    public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRunRecord>
    {
        public void Configure(EntityTypeBuilder<AgentRunRecord> builder)
        {
            builder.ToTable("AgentRuns");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever(); // the runner creates the id

            builder.Property(r => r.SessionId).IsRequired().HasMaxLength(64);
            builder.Property(r => r.UserEmail).HasMaxLength(256);
            builder.Property(r => r.Channel).IsRequired().HasMaxLength(20);
            builder.Property(r => r.Question).IsRequired();
            builder.Property(r => r.Answer).IsRequired();
            builder.Property(r => r.BlockedBy).HasMaxLength(50);
            builder.Property(r => r.StopReason).HasMaxLength(30);
            builder.Property(r => r.Error).HasMaxLength(200);
            builder.Property(r => r.Model).IsRequired().HasMaxLength(50);
            builder.Property(r => r.EstimatedCostUsd).HasColumnType("numeric(10,6)");

            builder.Property(r => r.ToolsUsed).HasJsonColumn();
            builder.Property(r => r.Steps).HasJsonColumn();
            builder.Property(r => r.GuardrailChecks).HasJsonColumn();
            builder.Property(r => r.Sources).HasJsonColumn();

            builder.HasIndex(r => r.CreatedAt); // the runs list is newest first
            builder.HasIndex(r => r.EvalRunId); // "show the runs of this eval"
        }
    }
}
